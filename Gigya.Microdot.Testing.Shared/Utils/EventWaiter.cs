#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.Testing.Shared.Utils
{
    public static class EventWaiterExtensions
    {
        public static async Task<T> WhenEventReceived<T>(this ISourceBlock<T> sourceBlock, TimeSpan? timeout = null)
        {
            var countDown = StartCountingEvents(sourceBlock);
            var result = await countDown.WhenNextEventReceived(timeout);
            countDown.Dispose();
            return result;
        }

        public static EventWaiter<T> StartCountingEvents<T>(this ISourceBlock<T> sourceBlock)
        {
            return new EventWaiter<T>(sourceBlock);            
        }
    }

    public sealed class EventWaiter<T>: IDisposable
    {
        private readonly List<T> _events = new List<T>();
        private readonly List<KeyValuePair<int, TaskCompletionSource<List<T>>>> _waiting = new List<KeyValuePair<int, TaskCompletionSource<List<T>>>>();

        public EventWaiter()
        {
        }

        public EventWaiter(ISourceBlock<T> sourceBlock)
        {
            _sourceBlockLink = sourceBlock.LinkTo(new ActionBlock<T>(_ => this.ReceivedEvent(_)));            
        }

        public async Task<T> WhenNextEventReceived(TimeSpan? timeout = null)
        {
            return (await WhenEventsReceived(null, timeout)).Last();
        }

        private readonly object _locker = new object();
        private readonly IDisposable _sourceBlockLink;

        public Task<List<T>> WhenEventsReceived(int? expectedNumberOfEvents, TimeSpan? timeout)
        {
            timeout = timeout ?? TimeSpan.FromSeconds(5);
            lock (_locker)
            {
                expectedNumberOfEvents = expectedNumberOfEvents ?? _events.Count + 1;
                if (_events.Count >= expectedNumberOfEvents)
                {
                    Console.WriteLine($"already received {_events.Count} events");
                    return Task.FromResult(_events.ToList());
                }

                var cancel = new CancellationTokenSource(timeout.Value);
                var wait = new TaskCompletionSource<List<T>>();
                cancel.Token.Register(() => wait.TrySetException( new Exception( $"Expected events: {expectedNumberOfEvents}. " +
                                                                                 $"Received events: {_events.Count}. " +
                                                                                 $"Timeout after {timeout.Value.TotalMilliseconds} ms")));
                wait.Task.ContinueWith(x => cancel.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);

                _waiting.Add(new KeyValuePair<int, TaskCompletionSource<List<T>>>(expectedNumberOfEvents.Value, wait));

                return wait.Task;
            }
        }
        
        public void ReceivedEvent(T @event)
        {
            try
            {
                lock (_locker)
                {
                    _events.Add(@event);

                    Console.WriteLine($"Received new event, total events {_events.Count}. EventDescription: {@event}");

                    for (int i = 0; i < _waiting.Count; i++)
                    {
                        var wait = _waiting[i];
                        if (_events.Count >= wait.Key) wait.Value.TrySetResult(_events.ToList());
                    }


                    _waiting.RemoveAll(x => x.Value.Task.IsCompleted);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public List<T> ReceivedEvents
        {
            get
            {
                lock (_locker)
                {
                    return _events.ToList();
                }
            }
        }

        public void Dispose()
        {
            _sourceBlockLink?.Dispose();
        }
    }
    }