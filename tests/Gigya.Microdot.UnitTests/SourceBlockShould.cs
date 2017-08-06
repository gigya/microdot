using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.UnitTests
{
    public static class SourceBlockShould
    {
        private static readonly TimeSpan _defaultMaxTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _defaultMinTimeout = TimeSpan.FromMilliseconds(500);

        public static async Task<SourceBlockMessage<T>> ShouldRaiseMessage<T>(this ISourceBlock<T> sourceBlock, Action actionWhichShouldSendMessage, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
                timeout = _defaultMaxTimeout;

            var messageSent = false;
            var message = default(T);
            using (sourceBlock.LinkTo(new ActionBlock<T>(m => {
                messageSent = true;
                message = m;
            })))
            {
                var task = new Task(actionWhichShouldSendMessage);
                task.Start();
                await task;

                var timeoutDateTime = DateTime.Now + timeout;
                while (!messageSent)
                {
                    await Task.Delay(10);
                    if (DateTime.Now > timeoutDateTime)
                        throw new Exception("timeout: source block did not send message on time");
                }
                await Task.Delay(10); // wait a little more to enable all other code linked to this source to get finished.
            }
            return new SourceBlockMessage<T>(message);
        }

        public static async Task ShouldNotRaiseMessage<T>(this ISourceBlock<T> sourceBlock, Action actionWhichShouldSendMessage, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
                timeout = _defaultMinTimeout;

            var messageSent = false;
            var message = default(T);
            using (sourceBlock.LinkTo(new ActionBlock<T>(m => {
                messageSent = true;
                message = m;
            })))
            {
                var task = new Task(actionWhichShouldSendMessage);
                task.Start();
                await task;

                await Task.Delay(timeout);

                if (messageSent)
                    throw new Exception("Recieved a message which should have NOT been sent");
            }
        }

    }



    public class SourceBlockMessage<T>
    {
        public T Message { get; private set; }

        public SourceBlockMessage(T message)
        {
            Message = message;
        }
    }
}
