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
using System.Linq;
using System.Text.RegularExpressions;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting.Events
{
    public class ServiceCallEvent : StatsEvent
    {
   
        protected Regex ExcludeStackTraceForErrorCodeRegex => Configuration.ExcludeStackTraceRule;

        internal double? ActualTotalTime { get; set; }

      
        public override double? TotalTime => ActualTotalTime;

        public override string EventType => EventConsts.ServerReqType;

        public TracingData ClientMetadata { get; set; }

        /// <summary>The interface of the service that executed executed the request</summary>
        [EventField(EventConsts.srvService)]
        internal string CalledServiceName { get; set; }

        /// <summary>The name of the calling  system (comments/socialize/hades/mongo etc)</summary>
        [EventField("cln.system")]
        public string CallerServiceName => ClientMetadata?.ServiceName;

        /// <summary>The name of a calling server</summary>    
        [EventField("cln.host")]
        public string CallerHostName => ClientMetadata?.HostName;

        /// <summary> Service method called </summary>
        [EventField(EventConsts.targetMethod)]
        public string ServiceMethod { get; set; }

        /// <summary> Service method arguments </summary>
        [EventField("params", Encrypt = true)]
        public IEnumerable<KeyValuePair<string, string>> ServiceMethodArguments => LazyRequestParams.GetValue(this);

    
        private readonly SharedLogic.Utils.Lazy<List<KeyValuePair<string, string>>, ServiceCallEvent> LazyRequestParams =
            new SharedLogic.Utils.Lazy<List<KeyValuePair<string, string>>, ServiceCallEvent>(this_ => this_.GetRequestParams().ToList());

        public IEnumerable<Param> Params { get; set; }


        private IEnumerable<KeyValuePair<string, string>> GetRequestParams()
        {
            
            if (!Configuration.ExcludeParams && Params != null)
            {
                foreach (var param in Params.Where(param => param.Value != null))
                {
                    var val = param.Value.Substring(0, Math.Min(param.Value.Length, Configuration.ParamTruncateLength));

                    yield return new KeyValuePair<string, string>(param.Name, val);
                }
            }
        }
    }
}