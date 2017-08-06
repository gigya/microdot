using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.UnitTests
{
    public static class CountDownEventExtensions
    {
        public static async Task<T> WhenEventReceived<T>(this ISourceBlock<T> sourceBlock, TimeSpan? timeout = null)
        {
            var countDown = StartCountingEvents(sourceBlock);
            return await countDown.WhenNextEventReceived(timeout);            
        }

        public static CountDownEvent<T> StartCountingEvents<T>(this ISourceBlock<T> sourceBlock)
        {
            var countDown = new CountDownEvent<T>();
            sourceBlock.LinkTo(new ActionBlock<T>(_ => countDown.ReceivedEvent(_)));
            return countDown;
        }
    }

    public class CountDownEvent<T>
    {
        private readonly List<T> _events = new List<T>();
        private readonly List<KeyValuePair<int, TaskCompletionSource<List<T>>>> _waiting = new List<KeyValuePair<int, TaskCompletionSource<List<T>>>>();

        public async Task<T> WhenNextEventReceived(TimeSpan? timeout = null)
        {
            return (await WhenEventsReceived(null, timeout)).Last();
        }

        private readonly object _locker = new object();
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
                cancel.Token.Register(
                    () => wait.TrySetException(
                        new Exception(
                            $"Expected events: {expectedNumberOfEvents}. Received events: {_events.Count}. Timeout after {timeout.Value.TotalMilliseconds} ms")));
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

    }
    }