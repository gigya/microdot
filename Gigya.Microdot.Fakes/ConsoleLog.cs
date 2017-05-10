using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic.Logging;

namespace Gigya.Microdot.Fakes
{
    /// <summary>
    /// A simple log implementation that writes to everything to the console.
    /// </summary>
    public class ConsoleLog : FakeLog
    {
        protected override Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo, string message,
                                               List<KeyValuePair<string, string>> encTags,
                                               List<KeyValuePair<string, string>> unencTags, Exception exception = null, string stackTrace = null)
        {
            var log = FormatLogEntry(level, message, encTags.Concat(unencTags)
                                                                 .Where(_ => _.Value != null)
                                                                 .ToList(), exception);            
            Console.WriteLine(log);

            return Task.FromResult(true);
        }        
    }

}