using System;
using System.Diagnostics;
using Gigya.Microdot.Interfaces.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace Gigya.Microdot.Orleans.Hosting.Logging
{
    public class OrleansLogAdapter : ILogger, ILog
    {
        private readonly ILog _logImplementation;

        public OrleansLogAdapter(string category, Func<string, ILog> logImplementation)
        {
            _logImplementation = logImplementation(category);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {

            var logMessage = formatter(state, exception);

            Action<LogDelegate> action = _ => _(logMessage, exception: exception, unencryptedTags: new { eventId.Id, eventId.Name, IsOrleansLog = true });
            var level = TraceEventType.Critical;
            switch (logLevel)
            {
                case LogLevel.Critical:
                    level = TraceEventType.Critical;
                    break;
                case LogLevel.Debug:
                    level = TraceEventType.Verbose;
                    break;
                case LogLevel.Trace:
                    level = TraceEventType.Verbose;
                    break;
                case LogLevel.Information:
                    level = TraceEventType.Information;
                    break;
                case LogLevel.Error:
                    level = TraceEventType.Error;
                    break;
                case LogLevel.Warning:
                    level = TraceEventType.Warning;
                    break;
                case LogLevel.None:
                    return;
            }
            _logImplementation.Write(level, action);

        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public void Debug(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Debug(log, file, line, method);
        }

        public void Info(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Info(log, file, line, method);
        }

        public void Warn(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Warn(log, file, line, method);
        }

        public void Warn(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null,
            bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Warn(message, encryptedTags, unencryptedTags, exception, includeStack, file, line, method);
        }

        public void Error(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Error(log, file, line, method);
        }

        public void Error(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null,
            bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Error(message, encryptedTags, unencryptedTags, exception, includeStack, file, line, method);
        }

        public void Critical(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Critical(log, file, line, method);
        }

        public void Critical(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null,
            bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Critical(message, encryptedTags, unencryptedTags, exception, includeStack, file, line, method);
        }

        public void Write(TraceEventType level, Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Write(level, log, file, line, method);
        }
    }
}