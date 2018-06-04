using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <inheritdoc />
    internal sealed class Discovery : IDiscovery
    {
        private object _nodeSourcesLocker = new object();
        private Task _cleanupTask;
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private Func<DeploymentIdentifier, ReachabilityCheck, TrafficRouting, ILoadBalancer> CreateLoadBalancer { get; }
        private IDateTime DateTime { get; }
        private Func<DeploymentIdentifier, LocalNodeSource> CreateLocalNodeSource { get; }        
        private Func<DeploymentIdentifier, ConfigNodeSource> CreateConfigNodeSource { get; }
        private Func<DeploymentIdentifier, ConsulQueryNodeSource> CreateConsulQueryNodeSource { get; }
        private Func<DiscoveryConfig> GetConfig { get; }

        private Dictionary<string, INodeSourceFactory> NodeSourceFactories { get; }
        private ConcurrentDictionary<DeploymentIdentifier, Lazy<Task<INodeSource>>> NodeSources { get; }
        private ConcurrentDictionary<DeploymentIdentifier, DateTime> NodeSourceLastRequested { get; }
        /// <inheritdoc />
        public Discovery(Func<DiscoveryConfig> getConfig, 
            Func<DeploymentIdentifier, ReachabilityCheck, TrafficRouting, ILoadBalancer> createLoadBalancer, 
            IDateTime dateTime,
            INodeSourceFactory[] nodeSourceFactories, 
            Func<DeploymentIdentifier, LocalNodeSource> createLocalNodeSource, 
            Func<DeploymentIdentifier, ConfigNodeSource> createConfigNodeSource,
            Func<DeploymentIdentifier, ConsulQueryNodeSource> createConsulQueryNodeSource)
        {
            CreateConsulQueryNodeSource = createConsulQueryNodeSource;
            GetConfig = getConfig;
            CreateLoadBalancer = createLoadBalancer;
            DateTime = dateTime;
            CreateLocalNodeSource = createLocalNodeSource;
            CreateConfigNodeSource = createConfigNodeSource;
            NodeSourceFactories = nodeSourceFactories.ToDictionary(factory => factory.Type);
            NodeSources = new ConcurrentDictionary<DeploymentIdentifier, Lazy<Task<INodeSource>>>();
            NodeSourceLastRequested = new ConcurrentDictionary<DeploymentIdentifier, DateTime>();
            _cleanupTask = Task.Run(CleanupLoop);
        }

        /// <inheritdoc />
        public async Task<ILoadBalancer> TryCreateLoadBalancer(DeploymentIdentifier deploymentIdentifier, ReachabilityCheck reachabilityCheck, TrafficRouting trafficRouting)
        {
            var nodes = await GetNodes(deploymentIdentifier);
            if (nodes != null)
                return CreateLoadBalancer(deploymentIdentifier, reachabilityCheck, trafficRouting);
            else
                return null;
        }

        /// <inheritdoc />
        public async Task<Node[]> GetNodes(DeploymentIdentifier deploymentIdentifier)
        {
            NodeSourceLastRequested.AddOrUpdate(deploymentIdentifier, DateTime.UtcNow, (_,__) => DateTime.UtcNow);

            INodeSource nodeSource = null;            
            if (NodeSources.TryGetValue(deploymentIdentifier, out Lazy<Task<INodeSource>> getNodeSourceTask))
            {
                nodeSource = await getNodeSourceTask.Value.ConfigureAwait(false);
                // for an existing nodeSource, if it is was undeployed or is deprecated, we need to reload it in order to check if it was re-deployed                                                
                if (nodeSource==null || nodeSource.WasUndeployed || IsDeprecated(nodeSource, deploymentIdentifier))
                {
                    if (NodeSourceMayBeCreated(deploymentIdentifier))
                    {
                        Lazy<Task<INodeSource>> getNewNodeSourceTask;
                        lock (_nodeSourcesLocker)
                        {
                            // if no other thread has updated the dictionary with a new nodeSource, then try create a new nodeSource                        
                            if (NodeSources.TryGetValue(deploymentIdentifier, out getNewNodeSourceTask))
                                if (getNewNodeSourceTask == getNodeSourceTask)
                                {
                                    getNewNodeSourceTask = NodeSources[deploymentIdentifier] =
                                        new Lazy<Task<INodeSource>>(()=> TryCreateNodeSource(deploymentIdentifier));
                                    nodeSource?.Dispose();
                                }
                        }
                        nodeSource = await getNewNodeSourceTask.Value.ConfigureAwait(false);
                    }
                    else // NodeSource cannot be created. Don't try to re-create the nodeSource
                    {
                        if (nodeSource != null)
                            lock (_nodeSourcesLocker)
                            {
                                if (NodeSources.TryGetValue(deploymentIdentifier, out var getNewNodeSourceTask))
                                    if (getNewNodeSourceTask == getNodeSourceTask)
                                        if (!nodeSource.WasUndeployed)
                                            nodeSource.Dispose();                            
                                NodeSources[deploymentIdentifier] = new Lazy<Task<INodeSource>>(()=> Task.FromResult<INodeSource>(null));
                            }
                    }
                }
            }
            else 
            {
                // nodeSource not exists on cache. Try create a new nodeSource
                getNodeSourceTask = NodeSources.GetOrAdd(deploymentIdentifier, _ => new Lazy<Task<INodeSource>>(()=> TryCreateNodeSource(deploymentIdentifier)));
                nodeSource = await getNodeSourceTask.Value.ConfigureAwait(false);
            }
            if (nodeSource==null || nodeSource.WasUndeployed)
                return null;
            else
                return nodeSource.GetNodes();
        }

        private bool IsDeprecated(INodeSource nodeSource, DeploymentIdentifier deploymentIdentifier)
        {
            var expectedSourceType = GetSourceType(deploymentIdentifier);
            return nodeSource.Type != expectedSourceType;
        }

        /// <inheritdoc />
        private async Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier deploymentIdentifier)
        {
            INodeSource nodeSource;
            var sourceType = GetSourceType(deploymentIdentifier);
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
                    else throw new ConfigurationException($"Discovery Source '{sourceType}' is not supported.");
                    break;
            }

            return nodeSource;        
        }

        private bool NodeSourceMayBeCreated(DeploymentIdentifier deploymentIdentifier)
        {
            var sourceType = GetSourceType(deploymentIdentifier);
            switch (sourceType)
            {
                case "Config":
                case "Local":
                    return true;
                case "ConsulQuery":
                    if (!NodeSources.TryGetValue(deploymentIdentifier, out var nodeSource) 
                        || !nodeSource.Value.IsCompleted 
                        || nodeSource.Value.Result.Type != "ConsulQuery")
                        return true;
                    return !nodeSource.Value.Result.WasUndeployed;
                default:
                    if (NodeSourceFactories.TryGetValue(sourceType, out var factory))
                        return factory.MayCreateNodeSource(deploymentIdentifier);
                    else throw new ConfigurationException($"Discovery Source '{sourceType}' is not supported.");                    
            }
        }

        private string GetSourceType(DeploymentIdentifier deploymentIdentifier)
        {
            var serviceConfig = GetConfig().Services[deploymentIdentifier.ServiceName];
            return serviceConfig.Source;
        }

        private async Task CleanupLoop()
        {
            while (!_shutdownTokenSource.Token.IsCancellationRequested)
            {
                var lifetime = GetConfig().MonitoringLifetime;
                foreach (var nodeSourceLastRequested in NodeSourceLastRequested.ToArray())
                {
                    if (nodeSourceLastRequested.Value + lifetime < DateTime.UtcNow)
                    {
                        if (NodeSources.TryRemove(nodeSourceLastRequested.Key, out var getNodeSourceTask))
                        {
                            if (getNodeSourceTask.Value.IsCompleted && getNodeSourceTask.Value.Result is IDisposable disposableNodeSource)
                                disposableNodeSource.Dispose();

                            NodeSourceLastRequested.TryRemove(nodeSourceLastRequested.Key, out var _);                            
                        }
                    }
                }
                await DateTime.Delay(TimeSpan.FromSeconds(30), _shutdownTokenSource.Token);
            }
        }

        public void Dispose()
        {
            _shutdownTokenSource.Cancel();
            _shutdownTokenSource.Dispose();
            _cleanupTask.Dispose();
        }
    }
}
