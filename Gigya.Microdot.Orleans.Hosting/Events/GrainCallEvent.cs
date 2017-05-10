using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Orleans.Hosting.Events
{
    public class GrainCallEvent : StatsEvent
    {
        public override string FlumeType => EventConsts.GrainReqType;

        [FlumeField(EventConsts.targetType)]
        public string TargetType { get; set; }

        [FlumeField(EventConsts.targetMethod)]
        public string TargetMethod { get; set; }
    }
}
