using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic.Logging;

namespace Gigya.Microdot.Fakes
{
    public class NullLog : LogBase
    {

        protected override Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo, string message, List<KeyValuePair<string, string>> encTags, List<KeyValuePair<string, string>> unencTags, Exception exception = null, string stackTrace = null)
        {
            return Task.FromResult(true);
        }


        public override TraceEventType? MinimumTraceLevel { get { return TraceEventType.Verbose; } set{} }
    }
}