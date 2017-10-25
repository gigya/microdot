using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.ServiceDiscovery
{
    public interface IDiscoverySourceFactory
    {
        string SourceName { get; }

        ServiceDiscoverySourceBase CreateSource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoveryConfig);
    }
}