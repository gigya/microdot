using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.Fakes.Discovery
{
    public class AlwaysLocalHost : IDiscoverySourceLoader
    {
        public ServiceDiscoverySourceBase GetDiscoverySource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoveryConfig)
        {
            return new LocalDiscoverySource(serviceDeployment.ServiceName);
        }
    }
}