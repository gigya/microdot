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
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
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
        private readonly IDiscovery _discovery;
        private Func<DiscoveryConfig> GetConfig { get; }

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private readonly ReachabilityCheck _reachabilityCheck; 

        private readonly IEnvironment _environment;
        private AggregatingHealthStatus _aggregatingHealthStatus;

        private readonly ConcurrentDictionary<string, ILoadBalancer> _loadBalancersByEnvironment;
        private readonly ConcurrentDictionary<string, Func<HealthCheckResult>> _helthCheckByEnvironment;
        private readonly ConcurrentDictionary<string, IDisposable> _disposableHelthChecksByEnvironment;
        
        public MultiEnvironmentServiceDiscovery(string serviceName,
                                ReachabilityCheck reachabilityCheck,
                                IEnvironment environment,
                                Func<DiscoveryConfig> discoveryConfigFactory,
                                IDiscovery discovery,
                                Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            _discovery = discovery;            
            _serviceName = serviceName;
            _environment = environment;

            _reachabilityCheck = reachabilityCheck;
            GetConfig = discoveryConfigFactory;

            _loadBalancersByEnvironment = new ConcurrentDictionary<string, ILoadBalancer>();
            _helthCheckByEnvironment = new ConcurrentDictionary<string, Func<HealthCheckResult>>();
            _disposableHelthChecksByEnvironment = new ConcurrentDictionary<string, IDisposable>();

            _aggregatingHealthStatus = getAggregatingHealthStatus("Discovery");
        }

        ///<inheritdoc />
        public async Task<Tuple<Node, ILoadBalancer>> GetNode()
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

        private async Task<Tuple<Node, ILoadBalancer>> GetNodeWithOverridenHost()
        {
            ILoadBalancer loadBalancer = new OverridenLoadBalancer(_serviceName);
            Node node = await loadBalancer.TryGetNode();

            return new Tuple<Node, ILoadBalancer>(node, loadBalancer);
        }

        private async Task<Tuple<Node, ILoadBalancer>> GetNodeFromPreferedEnvironment(string preferedEnvironment)
        {
            ILoadBalancer loadBalancer = GetOrCreateLoadBalancer(preferedEnvironment);
            RegisterHealthCheckIfNeeded(preferedEnvironment);

            Node node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node == null)
            {
                loadBalancer = GetOrCreateLoadBalancer(MASTER_ENVIRONMENT);
                node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            }

            if (node == null)
            {
                UpdateHealthCheck(preferedEnvironment, BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed neither on '{preferedEnvironment}' or '{MASTER_ENVIRONMENT}'")));
                throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", preferedEnvironment }, { "masterEnvironment", MASTER_ENVIRONMENT } });
            }

            UpdateHealthCheck(preferedEnvironment, () => HealthCheckResult.Healthy($"Discovered on '{preferedEnvironment}'"));

            return new Tuple<Node, ILoadBalancer>(node, loadBalancer);
        }

        private void UpdateHealthCheck(string environment, Func<HealthCheckResult> healthCheckResult)
        {
            Func<HealthCheckResult> hcResult;
            _helthCheckByEnvironment.TryGetValue(environment, out hcResult);
            _helthCheckByEnvironment.TryUpdate(environment, healthCheckResult, hcResult);
        }

        private void RegisterHealthCheckIfNeeded(string environment)
        {
            if (_helthCheckByEnvironment.TryAdd(environment,
                () => HealthCheckResult.Healthy("Initializing. Service was not discovered yet")))
            {
                IDisposable healthCheck = _aggregatingHealthStatus.RegisterCheck($"{_serviceName}-{environment}", () =>
                {
                    _helthCheckByEnvironment.TryGetValue(environment, out var helthStatus);
                    return helthStatus();
                });

                _disposableHelthChecksByEnvironment.TryAdd(environment, healthCheck);
            }
        }

        private async Task<Tuple<Node, ILoadBalancer>> GetNodeFromOriginatingEnvironment()
        {
            ILoadBalancer loadBalancer = GetOrCreateLoadBalancer(_environment.DeploymentEnvironment);
            RegisterHealthCheckIfNeeded(_environment.DeploymentEnvironment);
            Node node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node != null)
            {
                UpdateHealthCheck(_environment.DeploymentEnvironment, () => HealthCheckResult.Healthy($"Discovered on '{_environment.DeploymentEnvironment}'"));
                return new Tuple<Node, ILoadBalancer>(node, loadBalancer);
            }

            if (!GetConfig().EnvironmentFallbackEnabled)
            {
                UpdateHealthCheck(_environment.DeploymentEnvironment,
                    BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy(
                        $"Not deployed on '{_environment.DeploymentEnvironment}'. Deployement on '{MASTER_ENVIRONMENT}' is not used, because fallback is disabled by configuration")));
                throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", _environment.DeploymentEnvironment } });
            }

            loadBalancer = GetOrCreateLoadBalancer(MASTER_ENVIRONMENT);
            node = await loadBalancer.TryGetNode().ConfigureAwait(false);
            if (node != null)
            {
                UpdateHealthCheck(MASTER_ENVIRONMENT, () => HealthCheckResult.Healthy($"Discovered on '{MASTER_ENVIRONMENT}'"));
                return new Tuple<Node, ILoadBalancer>(node, loadBalancer);
            }

            UpdateHealthCheck(MASTER_ENVIRONMENT, BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed neither on '{_environment.DeploymentEnvironment}' or '{MASTER_ENVIRONMENT}'")));
            throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", _environment.DeploymentEnvironment }, { "masterEnvironment", MASTER_ENVIRONMENT } });
        }

        private ILoadBalancer GetOrCreateLoadBalancer(string environment)
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

            foreach (KeyValuePair<string, IDisposable> keyValue in _disposableHelthChecksByEnvironment)
            {
                keyValue.Value.Dispose();
            }
        }
    }


}