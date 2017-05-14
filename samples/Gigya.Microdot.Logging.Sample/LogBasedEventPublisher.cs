using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.Logging
{
    public class LogBasedEventPublisher : IEventPublisher
    {
        private ILog Log { get; }

        private readonly FlumePublishingTasks publishingTasks = new FlumePublishingTasks
        {
            ToFlume = Task.FromResult(true),
            ToFlumeAudit = Task.FromResult(false)
        };

        public LogBasedEventPublisher(ILog log) { Log = log; }


        public FlumePublishingTasks TryPublish(IEvent evt)
        {
            Log.Debug(l => l("tracing", unencryptedTags: evt));

            return publishingTasks;
        }
    }
}