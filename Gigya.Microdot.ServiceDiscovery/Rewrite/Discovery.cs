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
    /// <remarks>it currently takes this class 1 second to detect that a service was undeployed or the confiugred source type changed.
    /// During that time, the last known good nodes are returned.</remarks>
    internal sealed class Discovery : IDiscovery
    {

        private Func<DeploymentIdentifier, ReachabilityCheck, TrafficRoutingStrategy, ILoadBalancer> _createLoadBalancer { get; }
        private IDateTime DateTime { get; }
        private Func<DiscoveryConfig> GetConfig { get; }
        private Func<DeploymentIdentifier, LocalNodeSource> CreateLocalNodeSource { get; }        
        private Func<DeploymentIdentifier, ConfigNodeSource> CreateConfigNodeSource { get; }
        private Dictionary<string, INodeSourceFactory> NodeSourceFactories { get; }

        class NodeSourceAndAccesstime
        {
            public string NodeSourceType;
            public Task<INodeSource> NodeSourceTask;
            public DateTime LastAccessTime;
        }

        private readonly ConcurrentDictionary<DeploymentIdentifier, Lazy<NodeSourceAndAccesstime>> _nodeSources
            = new ConcurrentDictionary<DeploymentIdentifier, Lazy<NodeSourceAndAccesstime>>();



        /// <inheritdoc />
        public Discovery(Func<DiscoveryConfig> getConfig, 
            Func<DeploymentIdentifier, ReachabilityCheck, TrafficRoutingStrategy, ILoadBalancer> createLoadBalancer, 
            IDateTime dateTime,
            INodeSourceFactory[] nodeSourceFactories, 
            Func<DeploymentIdentifier, LocalNodeSource> createLocalNodeSource, 
            Func<DeploymentIdentifier, ConfigNodeSource> createConfigNodeSource)
        {
            GetConfig = getConfig;
            _createLoadBalancer = createLoadBalancer;
            DateTime = dateTime;
            CreateLocalNodeSource = createLocalNodeSource;
            CreateConfigNodeSource = createConfigNodeSource;
            NodeSourceFactories = nodeSourceFactories.ToDictionary(factory => factory.Type, StringComparer.OrdinalIgnoreCase);
            Task.Run(() => CleanupLoop()); // Use default task scheduler
        }



        /// <inheritdoc />
        public ILoadBalancer CreateLoadBalancer(DeploymentIdentifier deploymentIdentifier, ReachabilityCheck reachabilityCheck, TrafficRoutingStrategy trafficRoutingStrategy)
        {
            return _createLoadBalancer(deploymentIdentifier, reachabilityCheck, trafficRoutingStrategy);
        }



        /// <inheritdoc />
        public async Task<Node[]> GetNodes(DeploymentIdentifier deploymentIdentifier)
        {
            // We have a cached node source; query it
            if (_nodeSources.TryGetValue(deploymentIdentifier, out Lazy<NodeSourceAndAccesstime> lazySource))
            {
                lazySource.Value.LastAccessTime = DateTime.UtcNow;
                var nodeSource = await lazySource.Value.NodeSourceTask.ConfigureAwait(false);
                return nodeSource.GetNodes();
            }

            // No node source but the service is deployed; create one and query it
            else if (await IsServiceDeployed(deploymentIdentifier).ConfigureAwait(false))
            {
                string sourceType = GetConfiguredSourceType(deploymentIdentifier);
                lazySource = _nodeSources.GetOrAdd(deploymentIdentifier, _ => new Lazy<NodeSourceAndAccesstime>(() =>
                    new NodeSourceAndAccesstime {
                        NodeSourceType = sourceType,
                        LastAccessTime = DateTime.UtcNow,
                        NodeSourceTask = CreateNodeSource(sourceType, deploymentIdentifier)
                    }));
                var nodeSource = await lazySource.Value.NodeSourceTask.ConfigureAwait(false);
                return nodeSource.GetNodes();
            }

            // No node source and the service is not deployed; return null
            else return null;
        }



        private async Task<bool> IsServiceDeployed(DeploymentIdentifier deploymentIdentifier)
        {
            var sourceType = GetConfiguredSourceType(deploymentIdentifier).ToLower();
            switch (sourceType)
            {
                case "config":
                case "local":
                    return true;
                default:
                    if (NodeSourceFactories.TryGetValue(sourceType, out var factory))
                        return await factory.IsServiceDeployed(deploymentIdentifier).ConfigureAwait(false);
                    else throw new ConfigurationException($"Discovery Source '{sourceType}' is not supported.");                    
            }
        }



        /// <inheritdoc />
        private async Task<INodeSource> CreateNodeSource(string sourceType, DeploymentIdentifier deploymentIdentifier)
        {
            INodeSource nodeSource;
            switch (sourceType.ToLower())
            {
                case "config":
                    nodeSource = CreateConfigNodeSource(deploymentIdentifier); break;
                case "local":
                    nodeSource = CreateLocalNodeSource(deploymentIdentifier); break;
                default:
                    if (NodeSourceFactories.TryGetValue(sourceType, out var factory))
                        nodeSource = await factory.CreateNodeSource(deploymentIdentifier).ConfigureAwait(false);
                    else throw new ConfigurationException($"Discovery Source '{sourceType}' is not supported.");
                    break;
            }

            return nodeSource;        
        }



        private string GetConfiguredSourceType(DeploymentIdentifier deploymentIdentifier)
        {
            var serviceConfig = GetConfig().Services[deploymentIdentifier.ServiceName];
            return serviceConfig.Source;
        }



        // Continuously scans the list of node sources and removes ones that haven't been used in a while or whose type
        // differs from configuration.
        private async void CleanupLoop()
        {
            while (!_shutdownTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var expiryTime = DateTime.UtcNow - GetConfig().MonitoringLifetime;

                    foreach (var nodeSource in _nodeSources)
                        if (   nodeSource.Value.Value.LastAccessTime < expiryTime
                            || nodeSource.Value.Value.NodeSourceType != GetConfiguredSourceType(nodeSource.Key)
                            || !await IsServiceDeployed(nodeSource.Key).ConfigureAwait(false))
                        {
    #pragma warning disable 4014
                            nodeSource.Value.Value.NodeSourceTask.ContinueWith(t => t.Result.Dispose());
    #pragma warning restore 4014
                            _nodeSources.TryRemove(nodeSource.Key, out _);
                        }

                    await DateTime.Delay(TimeSpan.FromSeconds(1), _shutdownTokenSource.Token);
                }
                catch {} // Shouldn't happen, but just in case. Cleanup musn't stop.
            }
        }



        private readonly CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();

        public void Dispose()
        {
            foreach (var sourceFactory in NodeSourceFactories)
            {
                sourceFactory.Value.Dispose();
            }

            _shutdownTokenSource.Cancel();
            _shutdownTokenSource.Dispose();
        }
    }
}
