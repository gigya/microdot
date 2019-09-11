using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Logging;
using NLog;

namespace Gigya.Microdot.Logging.NLog
{
    /// <summary>
    /// Writes logs using NLog.
    /// </summary>
    public class NLogLogger : LogBase
    {
        public override TraceEventType? MinimumTraceLevel { get; set; }

        private Logger Logger { get; }

        public NLogLogger(Type receivingType)
        {
            Logger = LogManager.GetLogger(receivingType.FullName);

            AssemblyName reflectedAssembly = receivingType.Assembly.GetName();
            CallSiteInfoTemplate = new LogCallSiteInfo
            {
                LoggerName = receivingType.Name,
                Namespace = receivingType.Namespace,
                ClassName = receivingType.Name,
                AssemblyName = reflectedAssembly.Name,
                AssemblyVersion = reflectedAssembly.Version.ToString(),
            };
        }

        public NLogLogger(string caller)
        {
            Logger = LogManager.GetLogger(caller);
        }

        protected override Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo,
            string message, IDictionary<string, string> encryptedTags, IDictionary<string, string> unencryptedTags,
            Exception exception = null, string stackTrace = null)
        {
            var logLevel = ToLogLevel(level);
            if (Logger.IsEnabled(logLevel))
            {
                var messageWithTags = message + ". " + string.Join(", ", unencryptedTags.Select(kvp => $"{kvp.Key.Substring(5)}={EventFieldFormatter.SerializeFieldValue(kvp.Value)}")) + ". ";
                Logger.Log(logLevel, exception, messageWithTags, null);
            }

            return Task.FromResult(true);
        }

        private LogLevel ToLogLevel(TraceEventType traceEventType)
        {
            switch (traceEventType)
            {
                case TraceEventType.Critical: return LogLevel.Fatal;
                case TraceEventType.Error: return LogLevel.Error;
                case TraceEventType.Warning: return LogLevel.Warn;
                case TraceEventType.Information: return LogLevel.Info;
                case TraceEventType.Verbose: return LogLevel.Debug;
                default: return LogLevel.Trace;
            }
        }
    }
}
