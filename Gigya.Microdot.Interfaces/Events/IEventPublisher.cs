
namespace Gigya.Microdot.Interfaces.Events
{
    public interface IEventPublisher
    {
        FlumePublishingTasks TryPublish(IEvent evt);
    }

    public interface IEventPublisher<T> where T : IEvent
    {
        FlumePublishingTasks TryPublish(T evt);
        T CreateEvent();
    }
}
