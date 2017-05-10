using System.Diagnostics;

using Gigya.Microdot.Interfaces.Events;

namespace Gigya.Microdot.SharedLogic.Events
{

    public class ClientCallEvent : Event
    {

        public ClientCallEvent()
        {
            ParentSpanId = TracingContext.TryGetSpanID();
        }

        public override string FlumeType => EventConsts.ClientReqType;

        /// <summary>The name of the calling  system (comments/socialize/hades/mongo etc)</summary>
        [FlumeField(EventConsts.targetService)]
        public virtual string TargetService { get; set; }

        /// <summary>The name of a calling server</summary>    
        [FlumeField(EventConsts.targetHost)]
        public string TargetHostName { get; set; }

        /// <summary>Service method called</summary>
        [FlumeField(EventConsts.targetMethod)]
        public string TargetMethod { get; set; }

        /// <summary>Stopwatch timestamp when request was sent.</summary>
        [FlumeField(EventConsts.clnSendTimestamp)]
        public long? RequestStartTimestamp { get; set; }

        /// <summary>Stopwatch timestamp when response was received. Not published to Flume, used in <see cref="TotalTimeMS"/>
        /// which is published.</summary>
        public long? ResponseEndTimestamp { get; set; }

        [FlumeField(EventConsts.protocolMethod)]
        public string ProtocolMethod { get; set; }

        [FlumeField(EventConsts.protocolParams)]
        public string ProtocolParams { get; set; }

        /// <summary>Total time in milliseconds from sending the request till we got a response.</summary>
        [FlumeField(EventConsts.statsTotalTime, OmitFromAudit = true)]
        public double? TotalTimeMS => (ResponseEndTimestamp - RequestStartTimestamp) / (Stopwatch.Frequency / 1000.0);

        /// <summary>Time in milliseconds on a server.</summary>
        [FlumeField(EventConsts.statsServerTime, OmitFromAudit = true)]
        public double? ServerTimeMs { get; set; }

        /// <summary>Total time - ServerTimeMs</summary>
        [FlumeField(EventConsts.statsNetworkTime, OmitFromAudit = true)]
        public double? NetworkTimeMS => TotalTimeMS - ServerTimeMs;

    }
}
