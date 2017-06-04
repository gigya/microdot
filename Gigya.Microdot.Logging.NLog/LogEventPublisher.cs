using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.Logging.NLog
{
    /// <summary>
    /// Publishes distributed tracing events to the log. Should only be used when no other event publishing system is available.
    /// </summary>
    public class LogEventPublisher : IEventPublisher
    {
        private ILog Log { get; }
        private IEnvironmentVariableProvider EnvProvider { get; }

        private PublishingTasks PublishingTasks { get; } = new PublishingTasks
        {
            PublishEvent = Task.FromResult(true),
            PublishAudit = Task.FromResult(false)
        };

        public LogEventPublisher(ILog log, IEnvironmentVariableProvider envProvider)
        {
            Log = log;
            EnvProvider = envProvider;
        }

        public PublishingTasks TryPublish(IEvent evt)
        {
            evt.Configuration = new EventConfig();
            evt.EnvironmentVariableProvider = EnvProvider;
            Log.Debug(l => l("Tracing event", unencryptedTags: evt));
            return PublishingTasks;
        }

        private class EventConfig : IEventConfiguration
        {
            public Regex ExcludeStackTraceRule { get; set; }
            public bool ExcludeParams { get; set; }
            public int ParamTruncateLength { get; set; }
        }
    }
}