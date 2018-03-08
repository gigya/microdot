using System;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Monitor;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulQueryNodeSource : ConsulNodeSource
    {
        public override string Type => "ConsulQuery";

        public ConsulQueryNodeSource(ServiceDeployment serviceDeployment, ConsulQueryClient consulClient, Func<ConsulConfig> getConfig, Func<string, AggregatingHealthStatus> getAggregatingHealthStatus) :
            base(serviceDeployment, consulClient, getConfig, getAggregatingHealthStatus)
        { }
    }
}