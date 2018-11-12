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
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Discovers nodes for service in the "preferred" or "originating" envrionment.
    /// If the service is not deployed on this environment, it may return nodes of this service on "prod" environment (in case EnvironmentFallbackEnabled="true")
    /// TODO: Delete this class after Discovery Rewrite is completed.
    /// </summary>
    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    public sealed class MultiEnvironmentServiceDiscovery : IMultiEnvironmentServiceDiscovery, IDisposable
    {        
        private const string MASTER_ENVIRONMENT = "prod";

        // Dependencies
        private readonly string ServiceName;
        private readonly IEnvironment Environment;
        private readonly ReachabilityCheck ReachabilityCheck;
        private readonly IDiscovery Discovery;
        private readonly Func<DiscoveryConfig> GetDiscoveryConfig;
        private ServiceDiscoveryConfig ServiceDiscoveryConfig => GetDiscoveryConfig().Services[ServiceName];

        // State
        private readonly ConcurrentDictionary<string, ILoadBalancer> _loadBalancers = new ConcurrentDictionary<string, ILoadBalancer>();
        private readonly ComponentHealthMonitor _healthMonitor;
        private HealthCheckResult _lastHealth = HealthCheckResult.Healthy();
        private DateTimeOffset _lastUsageTime = DateTimeOffset.MinValue;



        public MultiEnvironmentServiceDiscovery(string serviceName, IEnvironment environment, ReachabilityCheck reachabilityCheck,
                IDiscovery discovery, Func<DiscoveryConfig> getDiscoveryConfig, IHealthMonitor healthMonitor)
        {
            ServiceName = serviceName;
            Environment = environment;
            ReachabilityCheck = reachabilityCheck;
            Discovery = discovery;
            GetDiscoveryConfig = getDiscoveryConfig;

            _healthMonitor = healthMonitor.SetHealthFunction(serviceName, CheckHealth);
        }



        ///<inheritdoc />
        public async Task<NodeAndLoadBalancer> GetNode()
        {
            NodeAndLoadBalancer nodeAndLoadBalancer = null;

            // 1. Use explicit host override if provided in request
            var hostOverride = TracingContext.GetHostOverride(ServiceName);
            if (hostOverride != null)
                nodeAndLoadBalancer = new NodeAndLoadBalancer { Node = new Node(hostOverride.Hostname, hostOverride.Port), LoadBalancer = null };

            // 2. Otherwise, use preferred environment if provided in request
            string preferredEnvironment = TracingContext.GetPreferredEnvironment();
            if (nodeAndLoadBalancer == null && preferredEnvironment != null)
                nodeAndLoadBalancer = await GetNodeAndLoadBalancer(preferredEnvironment);

            // 3. Otherwise, use current environment
            if (nodeAndLoadBalancer == null)
                nodeAndLoadBalancer = await GetNodeAndLoadBalancer(Environment.DeploymentEnvironment);

            // 4. Otherwise, use production environment if configured and it's not our own environment
            if (nodeAndLoadBalancer == null && GetDiscoveryConfig().EnvironmentFallbackEnabled && Environment.DeploymentEnvironment != MASTER_ENVIRONMENT)
                nodeAndLoadBalancer = await GetNodeAndLoadBalancer(MASTER_ENVIRONMENT);

            // 5. Otherwise, throw an error and indicate in the health check that the service is not deployed
            if (nodeAndLoadBalancer == null)
            {
                _lastHealth = HealthCheckResult.Unhealthy("Service is not deployed");
                throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", ServiceName }, { "environment", Environment.DeploymentEnvironment } });
            }

            // All ok
            _lastHealth = HealthCheckResult.Unhealthy("Service is reachable");
            return nodeAndLoadBalancer;
        }



        private async Task<NodeAndLoadBalancer> GetNodeAndLoadBalancer(string environment)
        {
            var loadBalancer = _loadBalancers.GetOrAdd(environment, p => {
                var deploymentId = new DeploymentIdentifier(ServiceName, environment, Environment.Zone);
                return Discovery.CreateLoadBalancer(deploymentId, ReachabilityCheck, TrafficRoutingStrategy.RandomByRequestID);
            });
            Node node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node == null)
                return null;
            else return new NodeAndLoadBalancer { Node = node, LoadBalancer = loadBalancer };
        }



        private HealthCheckResult CheckHealth()
        {
            if (DateTimeOffset.UtcNow.Subtract(_lastUsageTime) > ServiceDiscoveryConfig.SuppressHealthCheckAfterServiceUnused)
                return HealthCheckResult.Healthy($"Health check suppressed because service was not in use for more than {ServiceDiscoveryConfig.SuppressHealthCheckAfterServiceUnused.TotalSeconds} seconds.");
            else return _lastHealth;
        }



        public void Dispose()
        {
            foreach (var loadBalancer in _loadBalancers.Values)
                loadBalancer.Dispose();
            _healthMonitor.Dispose();
        }
    }


}