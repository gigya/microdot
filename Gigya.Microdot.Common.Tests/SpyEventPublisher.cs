using System.Collections.Concurrent;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Events;
using NSubstitute;

namespace Gigya.Microdot.Common.Tests
{
    public class SpyEventPublisher : IEventPublisher
    {
        static readonly PublishingTasks PublishingTasks = new PublishingTasks { PublishEvent = Task.FromResult(true), PublishAudit = Task.FromResult(true) };
        public ConcurrentQueue<IEvent> Events = new ConcurrentQueue<IEvent>();

        public PublishingTasks TryPublish(IEvent evt)
        {
            evt.Configuration = Substitute.For<EventConfiguration>();
            evt.Configuration.ParamTruncateLength = 1000000; 
            Events.Enqueue(evt);
            return PublishingTasks;
        }

        public void Clear()
        {
            Events = new ConcurrentQueue<IEvent>();
        }
    }
}