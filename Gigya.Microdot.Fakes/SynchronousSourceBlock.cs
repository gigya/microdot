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
                if (!block.Post(messageValue))
                    throw new NotImplementedException();

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
