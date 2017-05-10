using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Logging;

namespace Gigya.Microdot.Fakes
{
    public class LogSpy : FakeLog
    {
        public class LogEntry
        {
            public TraceEventType Severity { get; set; }
            public string Message { get; set; }
            public Dictionary<string, string> EncryptedTags { get; set; }
            public Dictionary<string, string> UnencryptedTags { get; set; }
            public Exception Exception { get; set; }
        }

        private List<LogEntry> LogEntriesList { get; } = new List<LogEntry>();

        public IEnumerable<LogEntry> LogEntries
        {
            get
            {
                lock (LogEntriesList)
                {
                    return LogEntriesList;
                }
            }
        }


        protected override Task<bool> WriteLog(TraceEventType severity, LogCallSiteInfo logCallSiteInfo, string message, List<KeyValuePair<string, string>> encTags, List<KeyValuePair<string, string>> unencTags, Exception exception = null, string stackTrace = null)
        {            
            var entry = new LogEntry
            {
                Severity = severity,
                Message = message,
                EncryptedTags = encTags.ToDictionary(_ => _.Key, _ => EventFieldFormatter.SerializeFieldValue(_.Value)),
                UnencryptedTags = unencTags.ToDictionary(_ => _.Key, _ => EventFieldFormatter.SerializeFieldValue(_.Value)),
                Exception = exception
            };

            lock (LogEntriesList)
            {
                LogEntriesList.Add(entry);
            }

            return null;
        }

    }
}
