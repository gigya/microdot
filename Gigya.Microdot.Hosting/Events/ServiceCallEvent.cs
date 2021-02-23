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
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;

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

        /// <summary>The name of the calling  system (comments/s ocialize/hades/mongo etc)</summary>
        [EventField("cln.system")]
        public string CallerServiceName => ClientMetadata?.ServiceName;

        /// <summary>The name of a calling server</summary>    
        [EventField("cln.host")]
        public string CallerHostName => ClientMetadata?.HostName;

        /// <summary> Service method called </summary>
        [EventField(EventConsts.targetMethod)]
        public string ServiceMethod { get; set; }

        [EventField(EventConsts.protocolSchema)]
        public string ServiceMethodSchema { get; set; }

        /// <summary>  Sensitive Service method arguments </summary>
        [EventField("params", Encrypt = true)]
        public IEnumerable<KeyValuePair<string, object>> EncryptedServiceMethodArguments => LazyEncryptedRequestParams.GetValue(this);

        /// <summary> NonSensitive Service method arguments </summary>

        [EventField("params", Encrypt = false, TruncateIfLong = true)]
        public IEnumerable<KeyValuePair<string, object>> UnencryptedServiceMethodArguments => LazyUnencryptedRequestParams.GetValue(this);

        public double? ClientResponseTime { get; set; }

        /// <summary> The time, measured on response to client </summary>
        [EventField("stats.client.response.time")]
        public virtual double? ClientResponseTimeIfNeeded => ClientResponseTime >= Configuration.MinResponseTimeForLog ? ClientResponseTime : null;

        [EventField(EventConsts.SuppressCaching)]
        public CacheSuppress? SuppressCaching { get; set; }  

        private readonly SharedLogic.Utils.Lazy<List<KeyValuePair<string, object>>, ServiceCallEvent> LazyEncryptedRequestParams = new SharedLogic.Utils.Lazy<List<KeyValuePair<string, object>>, ServiceCallEvent>(this_ => this_.GetRequestParams(Sensitivity.Sensitive).ToList());
        private readonly SharedLogic.Utils.Lazy<List<KeyValuePair<string, object>>, ServiceCallEvent> LazyUnencryptedRequestParams = new SharedLogic.Utils.Lazy<List<KeyValuePair<string, object>>, ServiceCallEvent>(this_ => this_.GetRequestParams(Sensitivity.NonSensitive).ToList());


        public IEnumerable<Param> Params { get; set; }

        [EventField(EventConsts.RecvDateTicks)]
        public long RecvDateTicks { get; set; }

        [EventField(EventConsts.ReqStartupDeltaTicks)]
        public long ReqStartupDeltaTicks { get; set; }

        [EventField(EventConsts.TimeFromLastReq)]
        public long TimeFromLastReq { get; set; }

        [EventField(EventConsts.OutstandingRecvRequests)]
        public long? OutstandingRecvRequests { get; set; }

        [EventField(EventConsts.CollectionCountGen0)]
        public int? CollectionCountGen0 { get; set; }

        [EventField(EventConsts.CollectionCountGen1)]
        public int? CollectionCountGen1 { get; set; }

        [EventField(EventConsts.CollectionCountGen2)]
        public int? CollectionCountGen2 { get; set; }

        private IEnumerable<KeyValuePair<string, object>> GetRequestParams(Sensitivity sensitivity)
        {

            if (Params != null)
            {
                return Params.Where(param => !Configuration.ExcludeParams && Params != null)
                               .Where(param => param.Value != null && param.Sensitivity == sensitivity)
                               .Select(_ => new KeyValuePair<string, object>(_.Name, _.Value));
            }

            return Enumerable.Empty<KeyValuePair<string, object>>();

        }
    }
}