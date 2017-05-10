using System;

using Gigya.Microdot.Interfaces.Events;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class EventPublisher<T> : IEventPublisher<T> where T : IEvent
    {
        private IEventPublisher Publisher { get; }
        private Func<T> EventFactory { get; }


        public EventPublisher(IEventPublisher publisher, Func<T> eventFactory)
        {
            Publisher = publisher;
            EventFactory = eventFactory;

        }


        public FlumePublishingTasks TryPublish(T evt)
        {
            return Publisher.TryPublish(evt);
        }


        public T CreateEvent()
        {
            return EventFactory();
        }
    }
}