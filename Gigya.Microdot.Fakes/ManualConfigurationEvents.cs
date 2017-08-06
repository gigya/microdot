﻿#region Copyright 
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
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Fakes
{
   public class ManualConfigurationEvents : IConfigurationDataWatcher
    {
        private readonly IConfigEventFactory _eventFactory;
        private readonly BroadcastBlock<bool> block = new BroadcastBlock<bool>(null);

        public ManualConfigurationEvents(IConfigEventFactory eventFactory)
        {
            _eventFactory = eventFactory;
        }

        public void RaiseChangeEvent()
        {
            block.Post(true);
            // Task.Delay(100).Wait();
        }

        public Task<T> ChangeConfig<T>(TimeSpan? timeout) where T : IConfigObject
        {
            timeout = timeout ?? TimeSpan.FromSeconds(5);

            var cancel = new CancellationTokenSource(timeout.Value);

            TaskCompletionSource<T> waitForConfigChange = new TaskCompletionSource<T>();
            cancel.Token.Register(
                () => waitForConfigChange.TrySetException(
                    new Exception(
                        $"Expected config to change in time, Timeout after {timeout.Value.TotalMilliseconds} ms")));
            waitForConfigChange.Task.ContinueWith(x => cancel.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);

            var configEvent = _eventFactory.GetChangeEvent<T>();
            configEvent.LinkTo(new ActionBlock<T>(x =>
            {

                waitForConfigChange.TrySetResult(x);
            }));
            RaiseChangeEvent();
            
            return waitForConfigChange.Task;
        }

        public ISourceBlock<bool> DataChanges => block;
    }
}