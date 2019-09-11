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
        private const string DefaultEnvironmentFallbackTarget = "prod";

        // Dependencies
        private readonly string ServiceName;
        private readonly IEnvironment Environment;
        private readonly ReachabilityCheck ReachabilityCheck;
        private readonly IDiscovery Discovery;
        private readonly Func<DiscoveryConfig> GetDiscoveryConfig;
        private readonly IDateTime DateTime;

        // State
        private readonly ConcurrentDictionary<string, ILoadBalancer> _loadBalancers = new ConcurrentDictionary<string, ILoadBalancer>();
        private readonly IDisposable _healthCheck;
        private DateTime _lastUsageTime;
        private HealthMessage _healthStatus;


        public MultiEnvironmentServiceDiscovery(string serviceName, ReachabilityCheck reachabilityCheck,
                IDiscovery discovery, Func<DiscoveryConfig> getDiscoveryConfig, Func<string, AggregatingHealthStatus> getAggregatingHealthStatus,
                IDateTime dateTime, IEnvironment environment)
        {
            _healthStatus = new HealthMessage(Health.Info, message: null, suppressMessage: true);
            ServiceName = serviceName;
            Environment = environment;
            
            ReachabilityCheck = reachabilityCheck;
            Discovery = discovery;
            GetDiscoveryConfig = getDiscoveryConfig;
            DateTime = dateTime;
            _lastUsageTime = DateTime.UtcNow;
            var aggregatingHealthStatus = getAggregatingHealthStatus(serviceName);
            _healthCheck = aggregatingHealthStatus.Register(Environment.DeploymentEnvironment, CheckHealth);
        }

        ///<inheritdoc />
        public async Task<NodeAndLoadBalancer> GetNode()
        {
            _lastUsageTime = DateTime.UtcNow;
            NodeAndLoadBalancer nodeAndLoadBalancer = null;
            string preferredEnvironment = TracingContext.GetPreferredEnvironment();

            // 1. Use explicit host override if provided in request
            //    TBD: Theoretically if we only ever call a service through host overrides we might not have a health check for the service at all (though it is in use)
            var hostOverride = TracingContext.GetHostOverride(ServiceName); 
            if (hostOverride != null)
                return new NodeAndLoadBalancer {
                    Node = new Node(hostOverride.Host, hostOverride.Port),
                    LoadBalancer = null,
                    PreferredEnvironment = preferredEnvironment ?? Environment.DeploymentEnvironment,
                    TargetEnvironment = $"{hostOverride.Host}:{hostOverride.Port}"
                };

            // 2. Otherwise, use preferred environment if provided in request
            if (preferredEnvironment != null && (nodeAndLoadBalancer = await GetNodeAndLoadBalancer(preferredEnvironment, preferredEnvironment)) != null)
                return nodeAndLoadBalancer; 

            // 3. Otherwise, try use current environment
            if ((nodeAndLoadBalancer = await GetNodeAndLoadBalancer(Environment.DeploymentEnvironment, preferredEnvironment)) != null)
            {
                _healthStatus = new HealthMessage(Health.Healthy, message: null, suppressMessage: true); // No need for a health message since the load balancer we're returning already provides one
                return nodeAndLoadBalancer; 
            }

            var discoveryConfig = GetDiscoveryConfig();
            var fallbackTarget = discoveryConfig.EnvironmentFallbackTarget ?? DefaultEnvironmentFallbackTarget;

            // 4. We're in fallback env and service is not deployed, no fallback possible
            if (Environment.DeploymentEnvironment == fallbackTarget)
            {
                _healthStatus = new HealthMessage(Health.Unhealthy, "Service not deployed");
                throw ServiceNotDeployedException();
            }

            // 5. We're not in fallback env, but fallback is disabled
            if (discoveryConfig.EnvironmentFallbackEnabled == false)
            {
                _healthStatus = new HealthMessage(Health.Unhealthy, "Service not deployed (and fallback disabled)");
                throw ServiceNotDeployedException();
            }

            // 6. Otherwise, try fallback to fallback env
            if ((nodeAndLoadBalancer = await GetNodeAndLoadBalancer(fallbackTarget, preferredEnvironment ?? Environment.DeploymentEnvironment)) != null)
            {
                _healthStatus = new HealthMessage(Health.Healthy, $"Service not deployed to '{Environment.DeploymentEnvironment}' environment, falling back to '{fallbackTarget}' environment");
                return nodeAndLoadBalancer; 
            }
            
            _healthStatus = new HealthMessage(Health.Unhealthy, $"Service not deployed to '{Environment.DeploymentEnvironment}' environment, fallback enabled but service not deployed to '{fallbackTarget}' environment either");
            throw ServiceNotDeployedException();
        }

        private ServiceUnreachableException ServiceNotDeployedException()
        {
            return new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", ServiceName }, { "environment", Environment.DeploymentEnvironment } });
        }

        private async Task<NodeAndLoadBalancer> GetNodeAndLoadBalancer(string environment, string preferredEnvironment)
        {
            var loadBalancer = _loadBalancers.GetOrAdd(environment, p => {
                var deploymentId = new DeploymentIdentifier(ServiceName, environment, Environment.Zone);
                return Discovery.CreateLoadBalancer(deploymentId, ReachabilityCheck, TrafficRoutingStrategy.RandomByRequestID);
            });

            Node node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node == null)
                return null;

             return new NodeAndLoadBalancer { Node = node, LoadBalancer = loadBalancer, PreferredEnvironment = preferredEnvironment, TargetEnvironment = environment};
        }

        private HealthMessage CheckHealth()
        {
            var supressDuration = GetDiscoveryConfig().Services[ServiceName].SuppressHealthCheckAfterServiceUnused;
            if (DateTime.UtcNow.Subtract(_lastUsageTime) > supressDuration)
                return new HealthMessage(Health.Info, $"Service not in use for more than {supressDuration.TotalSeconds} seconds");

            return _healthStatus;
        }

        public void Dispose()
        {
            foreach (var loadBalancer in _loadBalancers.Values)
                loadBalancer.Dispose();

            _healthCheck.Dispose();
        }
    }
}