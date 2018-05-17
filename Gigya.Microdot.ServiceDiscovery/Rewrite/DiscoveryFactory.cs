using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <inheritdoc />
    internal class DiscoveryFactory : IDiscoveryFactory
    {
        private Func<DeploymentIdentifier, INodeSource, ReachabilityCheck, ILoadBalancer> CreateLoadBalancer { get; }
        private Func<DeploymentIdentifier, LocalNodeSource> CreateLocalNodeSource { get; }        
        private Func<DeploymentIdentifier, ConfigNodeSource> CreateConfigNodeSource { get; }
        private Func<DeploymentIdentifier, ConsulQueryNodeSource> CreateConsulQueryNodeSource { get; }
        private Func<DiscoveryConfig> GetConfig { get; }

        private Dictionary<string, INodeSourceFactory> NodeSourceFactories { get; }

        /// <inheritdoc />
        public DiscoveryFactory(Func<DiscoveryConfig> getConfig, 
            Func<DeploymentIdentifier, INodeSource, 
            ReachabilityCheck, ILoadBalancer> createLoadBalancer, 
            INodeSourceFactory[] nodeSourceFactories, 
            Func<DeploymentIdentifier, LocalNodeSource> createLocalNodeSource, 
            Func<DeploymentIdentifier, ConfigNodeSource> createConfigNodeSource,
            Func<DeploymentIdentifier, ConsulQueryNodeSource> createConsulQueryNodeSource)
        {
            CreateConsulQueryNodeSource = createConsulQueryNodeSource;
            GetConfig = getConfig;
            CreateLoadBalancer = createLoadBalancer;            
            CreateLocalNodeSource = createLocalNodeSource;
            CreateConfigNodeSource = createConfigNodeSource;
            NodeSourceFactories = nodeSourceFactories.ToDictionary(factory => factory.Type);
        }

        /// <inheritdoc />
        public async Task<ILoadBalancer> TryCreateLoadBalancer(DeploymentIdentifier deploymentIdentifier, ReachabilityCheck reachabilityCheck)
        {
            INodeSource nodeSource = await TryCreateNodeSource(deploymentIdentifier).ConfigureAwait(false);
            if (nodeSource != null)
                return CreateLoadBalancer(deploymentIdentifier, nodeSource, reachabilityCheck);

            return null;                          
        }

        /// <inheritdoc />
        public async Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier deploymentIdentifier)
        {
            INodeSource nodeSource;
            var serviceConfig = GetConfig().Services[deploymentIdentifier.ServiceName];
            var sourceType = serviceConfig.Source;
            switch (sourceType)
            {
                case "Config":
                    nodeSource = CreateConfigNodeSource(deploymentIdentifier); break;
                case "Local":
                    nodeSource = CreateLocalNodeSource(deploymentIdentifier); break;
                case "ConsulQuery":
                    var consulQueryNodeSource = CreateConsulQueryNodeSource(deploymentIdentifier);
                    await consulQueryNodeSource.Init().ConfigureAwait(false);
                    nodeSource = consulQueryNodeSource;      
                    break;
                default:
                    if (NodeSourceFactories.TryGetValue(sourceType, out var factory))
                        nodeSource = await factory.TryCreateNodeSource(deploymentIdentifier).ConfigureAwait(false);
                    else throw new ConfigurationException($"Discovery Source '{serviceConfig.Source}' is not supported.");
                    break;
            }

            return nodeSource;        
        }
    }
}
