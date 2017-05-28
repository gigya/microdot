using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.Logging.NLog
{
    /// <summary>
    /// Publishes distributed tracing events to the log. Should be used when no other event publishing system is available.
    /// </summary>
    public class LogEventPublisher : IEventPublisher
    {
        private ILog Log { get; }

        private readonly PublishingTasks publishingTasks = new PublishingTasks
        {
            PublishEvent = Task.FromResult(true),
            PublishAudit = Task.FromResult(false)
        };

        public LogEventPublisher(ILog log) { Log = log; }


        public PublishingTasks TryPublish(IEvent evt)
        {
            Log.Debug(l => l("tracing", unencryptedTags: evt));

            return publishingTasks;
        }
    }
}