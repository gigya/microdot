#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

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
