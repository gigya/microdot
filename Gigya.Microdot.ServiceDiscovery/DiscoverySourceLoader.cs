using System;

using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class DiscoverySourceLoader : IDiscoverySourceLoader
    {
        public DiscoverySourceLoader(Func<string, ServiceDiscoveryConfig, ConfigDiscoverySource>  getConfigDiscoverySource,
                                     Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulDiscoverySource> getConsulDiscoverySourc)
        {
            _getConfigDiscoverySource = getConfigDiscoverySource;
            _getConsulDiscoverySource = getConsulDiscoverySourc;
        }

        private readonly Func<string, ServiceDiscoveryConfig, ConfigDiscoverySource> _getConfigDiscoverySource;
        private readonly Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulDiscoverySource> _getConsulDiscoverySource;

        public ServiceDiscoverySourceBase GetDiscoverySource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoverySettings)
        {
            switch (serviceDiscoverySettings.Source)
            {
                case DiscoverySource.Config:
                    return _getConfigDiscoverySource(serviceDeployment.ServiceName, serviceDiscoverySettings);
                case DiscoverySource.Consul:
                    return _getConsulDiscoverySource(serviceDeployment, serviceDiscoverySettings);
                case DiscoverySource.Local:
                    return new LocalDiscoverySource(serviceDeployment.ServiceName);
            }

            throw new ConfigurationException($"Source '{serviceDiscoverySettings.Source}' is not supported by any configuration.");
        }
    }

    public interface IDiscoverySourceLoader
    {
        ServiceDiscoverySourceBase GetDiscoverySource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoverySettings);
    }
}