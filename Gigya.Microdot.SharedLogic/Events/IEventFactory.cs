using Gigya.Microdot.Interfaces.Events;

namespace Gigya.Microdot.SharedLogic.Events
{
    /// <summary>
    /// </summary>
    public interface IEventFactory<out T> where T : IEvent
    {
        /// <summary>
        /// Creates the concrete event with contextual fields (callId, application info, etc).
        /// </summary>
        T CreateEvent();
    }
}