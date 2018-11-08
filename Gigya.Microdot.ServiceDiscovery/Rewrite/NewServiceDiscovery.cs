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
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;
using Nito.AsyncEx;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// This class is an adapter between old interface if IServiceDiscovery and new Discovery implementation 
    /// TODO: Delete this class after Discovery Rewrite is completed.
    /// </summary>
    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    public sealed class NewServiceDiscovery : INewServiceDiscovery, IDisposable
    {        
        internal DiscoveryConfig LastConfig { get; private set; }
        internal ServiceDiscoveryConfig LastServiceConfig { get; private set; }

        private ILog Log { get; }
        private readonly IDiscovery _discovery;
        private Func<DiscoveryConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private readonly ReachabilityCheck _reachabilityCheck; 

        private Func<HealthCheckResult> _getHealthStatus;
        private readonly IDisposable _healthCheck;

        private readonly IEnvironment _environment;
        private readonly ConcurrentDictionary<string, ILoadBalancer> _loadBalancersByEnvironment;

        public NewServiceDiscovery(string serviceName,
                                ReachabilityCheck reachabilityCheck,
                                IEnvironment environment,
                                Func<DiscoveryConfig> discoveryConfigFactory,
                                ILog log,
                                IDiscovery discovery,
                                Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            Log = log;
            _discovery = discovery;            
            _serviceName = serviceName;
            _environment = environment;

            _reachabilityCheck = reachabilityCheck;
            GetConfig = discoveryConfigFactory;

            _loadBalancersByEnvironment = new ConcurrentDictionary<string, ILoadBalancer>();
            CreateLoadBalancer(environment.DeploymentEnvironment);
            CreateLoadBalancer(MASTER_ENVIRONMENT);
            
            AggregatingHealthStatus = getAggregatingHealthStatus("Discovery");
            _getHealthStatus = () => HealthCheckResult.Healthy("Initializing. Service was not discovered yet");
            _healthCheck = AggregatingHealthStatus.RegisterCheck(_serviceName, ()=>_getHealthStatus());           
        }

        public async Task<KeyValuePair<Node, ILoadBalancer>> GetNode()
        {
            HostOverride hostOverride = TracingContext.GetHostOverride(_serviceName);
            if (!string.IsNullOrEmpty(hostOverride?.Hostname))
            {
                return await GetNodeWithOverridenHost();
            }

            string preferedEnvironment = TracingContext.GetPreferredEnvironment();
            if (!string.IsNullOrEmpty(preferedEnvironment))
            {
                return await GetNodeFromPreferedEnvironment(preferedEnvironment);
            }

            return await GetNodeFromOriginatingEnvironment();
        }

        private async Task<KeyValuePair<Node, ILoadBalancer>> GetNodeWithOverridenHost()
        {
            ILoadBalancer loadBalancer = new OverridenLoadBalancer(_serviceName);
            Node node = await loadBalancer.TryGetNode();

            return new KeyValuePair<Node, ILoadBalancer>(node, loadBalancer);
        }

        private async Task<KeyValuePair<Node, ILoadBalancer>> GetNodeFromPreferedEnvironment(string preferedEnvironment)
        {
            ILoadBalancer loadBalancer = _loadBalancersByEnvironment.GetOrAdd(preferedEnvironment, p =>
                {
                    DeploymentIdentifier preferedDeployment = new DeploymentIdentifier(_serviceName, preferedEnvironment, _environment);
                    return _discovery.CreateLoadBalancer(preferedDeployment, _reachabilityCheck, TrafficRoutingStrategy.RandomByRequestID);
                });

            Node node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node == null)
            {
                _loadBalancersByEnvironment.TryGetValue(MASTER_ENVIRONMENT, out loadBalancer);
                node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            }

            if (node == null)
            {
                _getHealthStatus = BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed neither on '{preferedEnvironment}' or '{MASTER_ENVIRONMENT}'"));
                throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", preferedEnvironment }, { "masterEnvironment", MASTER_ENVIRONMENT } });
            }

            _getHealthStatus = () => HealthCheckResult.Healthy($"Discovered on '{preferedEnvironment}'");
            return new KeyValuePair<Node, ILoadBalancer>(node, loadBalancer);
        }

        private async Task<KeyValuePair<Node, ILoadBalancer>> GetNodeFromOriginatingEnvironment()
        {
            ILoadBalancer loadBalancer;
            _loadBalancersByEnvironment.TryGetValue(_environment.DeploymentEnvironment, out loadBalancer);
            Node node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node != null)
            {
                _getHealthStatus = () => HealthCheckResult.Healthy($"Discovered on '{_environment.DeploymentEnvironment}'");
                return new KeyValuePair<Node, ILoadBalancer>(node, loadBalancer);
            }

            if (!GetConfig().EnvironmentFallbackEnabled)
            {
                _getHealthStatus = BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed on '{_environment.DeploymentEnvironment}'. Deployement on '{MASTER_ENVIRONMENT}' is not used, because fallback is disabled by configuration"));
                throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", _environment.DeploymentEnvironment } });
            }

            _loadBalancersByEnvironment.TryGetValue(MASTER_ENVIRONMENT, out loadBalancer);
            node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node != null)
            {
                _getHealthStatus = () => HealthCheckResult.Healthy($"Discovered on '{MASTER_ENVIRONMENT}'");
                return new KeyValuePair<Node, ILoadBalancer>(node, loadBalancer);
            }

            _getHealthStatus = BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed neither on '{_environment.DeploymentEnvironment}' or '{MASTER_ENVIRONMENT}'"));
            throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", _environment.DeploymentEnvironment }, { "masterEnvironment", MASTER_ENVIRONMENT } });
        }

        private ILoadBalancer CreateLoadBalancer(string environment)
        {
            return _loadBalancersByEnvironment.GetOrAdd(environment, p =>
            {
                DeploymentIdentifier preferedDeployment = new DeploymentIdentifier(_serviceName, environment, _environment);
                return _discovery.CreateLoadBalancer(preferedDeployment, _reachabilityCheck, TrafficRoutingStrategy.RandomByRequestID);
            });
        }
        
        private Func<HealthCheckResult> BadHealthForLimitedPeriod(HealthCheckResult unhealthy)
        {
            var lastBadStateTime = DateTime.UtcNow;
            return () =>
            {
                if (DateTime.UtcNow > lastBadStateTime.AddMinutes(10))
                    return HealthCheckResult.Healthy("Not requested for more than 10 minutes. " + unhealthy.Message);
                else
                    return unhealthy;
            };
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, ILoadBalancer> keyValuePair in _loadBalancersByEnvironment)
            {
                keyValuePair.Value.Dispose();
            }

            _healthCheck.Dispose();
        }
    }


}