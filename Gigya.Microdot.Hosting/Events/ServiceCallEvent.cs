using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.Hosting.Events
{
    public class ServiceCallEvent : StatsEvent
    {
   
        protected Regex FlumeExcludeStackTraceForErrorCodeRegex => Configuration.ExcludeStackTraceRule;

        internal double? ActualTotalTime { get; set; }

      
        public override double? TotalTime => ActualTotalTime;

        public override string FlumeType => EventConsts.ServerReqType;

        public TracingData ClientMetadata { get; set; }

        /// <summary>The interface of the service that executed executed the request</summary>
        [FlumeField(EventConsts.srvService)]
        internal string CalledServiceName { get; set; }

        /// <summary>The name of the calling  system (comments/socialize/hades/mongo etc)</summary>
        [FlumeField("cln.system")]
        public string CallerServiceName => ClientMetadata?.ServiceName;

        /// <summary>The name of a calling server</summary>    
        [FlumeField("cln.host")]
        public string CallerHostName => ClientMetadata?.HostName;

        /// <summary> Service method called </summary>
        [FlumeField(EventConsts.targetMethod)]
        public string ServiceMethod { get; set; }

        /// <summary> Service method arguments </summary>
        [FlumeField("params", Encrypt = true)]
        public IEnumerable<KeyValuePair<string, string>> ServiceMethodArguments => LazyRequestParams.GetValue(this);

    
        private readonly Lazy<List<KeyValuePair<string, string>>, ServiceCallEvent> LazyRequestParams =
            new Lazy<List<KeyValuePair<string, string>>, ServiceCallEvent>(this_ => this_.GetRequestParams().ToList());

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