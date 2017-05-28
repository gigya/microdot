using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
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
            Logger = LogManager.GetCurrentClassLogger(receivingType);

            AssemblyName reflectedAssembly = receivingType.Assembly.GetName();
            CallSiteInfoTemplate = new LogCallSiteInfo
            {
                ReflectedType = receivingType,
                ClassName = receivingType.Name,
                AssemblyName = reflectedAssembly.Name,
                AssemblyVersion = reflectedAssembly.Version.ToString(),
            };
        }

        protected override Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo,
            string message, IDictionary<string, string> encryptedTags, IDictionary<string, string> unencryptedTags,
            Exception exception = null, string stackTrace = null)
        {
            var logLevel = ToLogLevel(level);
            if (Logger.IsEnabled(logLevel))
                Logger.Log(logLevel, message);

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
