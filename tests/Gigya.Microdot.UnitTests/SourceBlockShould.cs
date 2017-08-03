using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.UnitTests
{
    public static class SourceBlockShould
    {
        private static readonly TimeSpan _defaultMaxTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _defaultMinTimeout = TimeSpan.FromMilliseconds(500);

        public static Task<SourceBlockMessage<T>> ShouldRaiseMessage<T>(this ISourceBlock<T> sourceBlock, TimeSpan timeout = default(TimeSpan))
        {
            return ShouldRaiseMessage(sourceBlock, null, timeout);
        }

        public static async Task<SourceBlockMessage<T>> ShouldRaiseMessage<T>(this ISourceBlock<T> sourceBlock, Action actionWhichShouldSendMessage, TimeSpan timeout = default(TimeSpan))
        {
            return (await ShouldRaiseMessage(sourceBlock, 1, actionWhichShouldSendMessage, timeout)).FirstOrDefault();
        }

        public static Task<IEnumerable<SourceBlockMessage<T>>> ShouldRaiseMessage<T>(this ISourceBlock<T> sourceBlock, int expectedTimes, TimeSpan timeout = default(TimeSpan))
        {
            return ShouldRaiseMessage(sourceBlock, expectedTimes, null, timeout);
        }

        public static async Task<IEnumerable<SourceBlockMessage<T>>> ShouldRaiseMessage<T>(this ISourceBlock<T> sourceBlock, int expectedTimes, Action actionWhichShouldSendMessage, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
                timeout = _defaultMaxTimeout;
            
            var messages = new List<T>();
            int counter = 0;
            using (sourceBlock.LinkTo(new ActionBlock<T>(m => {
                            counter++;
                            Console.WriteLine($"Received new message, total messages: {counter}. Message: {m}");                                        
                            messages.Add(m);})))
            {
                if (actionWhichShouldSendMessage != null)
                {
                    var task = new Task(actionWhichShouldSendMessage);
                    task.Start();
                    await task;
                }

                var timeoutDateTime = DateTime.Now + timeout;
                while (counter < expectedTimes)
                {
                    await Task.Delay(10);
                    if (DateTime.Now > timeoutDateTime)
                        if (expectedTimes==1)
                            throw new Exception($"timeout after {timeout}: source block did not send message on time");
                        else
                            throw new Exception($"timeout after {timeout}: source block expected to receive {expectedTimes} messages but received only {counter} messages.");
                }
            }
            await Task.Delay(50); // wait a little more to enable all other code linked to this source to get finished.
            return messages.Select(message=> new SourceBlockMessage<T>(message));
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
