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
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.Fakes
{
    /// <summary>
    /// <see cref="ISourceBlock{TOutput}"/> implementation which can send only one message but do it synchronously.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OneTimeSynchronousSourceBlock<T> : ISourceBlock<T>
    {
        private readonly List<ITargetBlock<T>> _targetBlocks = new List<ITargetBlock<T>>();

        /// <summary>
        /// Post a message and return only after all <see cref="ITargetBlock{TInput}"/> instances where notified.
        /// </summary>
        /// <param name="messageValue"></param>
        public void PostMessageSynced(T messageValue)
        {
            _targetBlocks.ForEach(block =>
            {
                block.Post(messageValue);
                //if (!)
                  //  throw new NotImplementedException();

                block.Complete();
                block.Completion.Wait();
            });
        }
        

        public void Complete()
        {
            throw new NotImplementedException();
        }


        public void Fault(Exception exception)
        {
            throw new NotImplementedException();
        }


        public Task Completion { get; }

        public IDisposable LinkTo(ITargetBlock<T> target, DataflowLinkOptions linkOptions)
        {
            _targetBlocks.Add(target);

            return new NotImplementedDisposable();
        }


        public T ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target, out bool messageConsumed)
        {
            throw new NotImplementedException();
        }


        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            throw new NotImplementedException();
        }


        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
        {
            throw new NotImplementedException();
        }



        class NotImplementedDisposable : IDisposable
        {

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }
    }

}
