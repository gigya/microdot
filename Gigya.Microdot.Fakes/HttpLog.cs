using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic.Logging;

namespace Gigya.Microdot.Fakes
{
    /// <summary>
    /// Log used in conjunction with ServiceTester class to send logs across app domains.
    /// </summary>
    public class HttpLog : FakeLog
    {
        private readonly HttpClient httpClient = new HttpClient();

        private string HttpLogUrl { get; }
        private readonly TraceEventType severityToTrace;

        public HttpLog()
            : this(TraceEventType.Verbose)
        {

        }

        public HttpLog(TraceEventType severity)
        {
            severityToTrace = severity;

            var value = AppDomain.CurrentDomain.GetData("HttpLogListenPort");

            if(value is int == false)
                throw new InvalidOperationException("HttpLog cannot be used without ServiceTester.");

            HttpLogUrl = $"http://localhost:{(int)value}/log";
        }


        public override TraceEventType? MinimumTraceLevel => severityToTrace;


        protected override Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo, string message,
                                               List<KeyValuePair<string, string>> encTags,
                                               List<KeyValuePair<string, string>> unencTags, Exception exception = null, string stackTrace = null)
        {
            var str = FormatLogEntry(level, message, encTags.Concat(unencTags)
                                                                 .Where(_ => _.Value != null)
                                                                 .ToList(), exception);
                       
            httpClient.PostAsync(HttpLogUrl, new StringContent($"{AppDomain.CurrentDomain.FriendlyName}: {str}"));

            return Task.FromResult(true);
        }        
    }
}
