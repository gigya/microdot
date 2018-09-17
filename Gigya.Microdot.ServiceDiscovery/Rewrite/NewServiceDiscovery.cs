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
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
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

        private readonly DeploymentIdentifier _masterDeployment;
        private readonly DeploymentIdentifier _originatingEnvironmentDeployment;
        private ILoadBalancer MasterEnvironmentLoadBalancer { get; set; }
        private ILoadBalancer OriginatingEnvironmentLoadBalancer { get; set; }

        private ILog Log { get; }
        private readonly IDiscovery _discovery;
        private Func<DiscoveryConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private readonly ReachabilityCheck _reachabilityCheck;        
        private readonly AsyncLock _asyncLocker = new AsyncLock();
        private readonly IDisposable _configBlockLink;
        private readonly Task _initTask;

        private Func<HealthCheckResult> _getHealthStatus;
        private readonly IDisposable _healthCheck;

        public NewServiceDiscovery(string serviceName,
                                ReachabilityCheck reachabilityCheck,
                                IEnvironment environment,
                                ISourceBlock<DiscoveryConfig> configListener,
                                Func<DiscoveryConfig> discoveryConfigFactory,
                                ILog log,
                                IDiscovery discovery,
                                Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            Log = log;
            _discovery = discovery;            
            _serviceName = serviceName;
            
            _originatingEnvironmentDeployment = new DeploymentIdentifier(serviceName, environment.DeploymentEnvironment, environment);
            _masterDeployment = new DeploymentIdentifier(serviceName, MASTER_ENVIRONMENT, environment);

            _reachabilityCheck = reachabilityCheck;
            GetConfig = discoveryConfigFactory;
            // Must be run in Task.Run() because of incorrect Orleans scheduling
            _initTask = Task.Run(() => ReloadRemoteHost(discoveryConfigFactory()));
            _configBlockLink = configListener.LinkTo(new ActionBlock<DiscoveryConfig>(ReloadRemoteHost));

            AggregatingHealthStatus = getAggregatingHealthStatus("Discovery");
            _healthCheck = AggregatingHealthStatus.RegisterCheck(_serviceName, _getHealthStatus);
            _getHealthStatus = ()=>HealthCheckResult.Healthy("Initializing. Service was not discovered yet");
        }

        public async Task<Node> GetNode()
        {
            await _initTask.ConfigureAwait(false);
            var node = await OriginatingEnvironmentLoadBalancer.GetNode().ConfigureAwait(false);
            if (node != null)
            {
                _getHealthStatus = () => HealthCheckResult.Healthy($"Discovered on '{_originatingEnvironmentDeployment.DeploymentEnvironment}'");
                return node;
            }

            if (!GetConfig().EnvironmentFallbackEnabled)
            {
                _getHealthStatus = BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed on '{_originatingEnvironmentDeployment.DeploymentEnvironment}'. Deployement on '{_masterDeployment.DeploymentEnvironment}' is not used, because fallback is disabled by configuration"));
                throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags{{"serviceName",_serviceName}, {"environment", _originatingEnvironmentDeployment.DeploymentEnvironment}});
            }

            node = await MasterEnvironmentLoadBalancer.GetNode().ConfigureAwait(false);
            if (node != null)
            {
                _getHealthStatus = () => HealthCheckResult.Healthy($"Discovered on '{_masterDeployment.DeploymentEnvironment}'");
                return node;
            }

            _getHealthStatus = BadHealthForLimitedPeriod(HealthCheckResult.Unhealthy($"Not deployed neither on '{_originatingEnvironmentDeployment.DeploymentEnvironment}' or '{_masterDeployment.DeploymentEnvironment}'"));
            throw new ServiceUnreachableException("Service is not deployed", unencrypted: new Tags { { "serviceName", _serviceName }, { "environment", _originatingEnvironmentDeployment.DeploymentEnvironment }, {"masterEnvironment", _masterDeployment.DeploymentEnvironment}});
        }

        private async Task ReloadRemoteHost(DiscoveryConfig discoveryConfig)
        {
            using (await _asyncLocker.LockAsync().ConfigureAwait(false))
            {
                if (discoveryConfig == LastConfig)
                    return;

                var newServiceConfig = discoveryConfig.Services[_serviceName];

                if (newServiceConfig.Equals(LastServiceConfig) &&
                    discoveryConfig.EnvironmentFallbackEnabled == LastConfig.EnvironmentFallbackEnabled)
                    return;

                LastConfig = discoveryConfig;
                LastServiceConfig = newServiceConfig;

                CreateOriginatingEnvironmentLoadBalancer();
                CreateMasterEnvironmentLoadBalancer();
            }
        }

        private void CreateMasterEnvironmentLoadBalancer()
        {
            if (_masterDeployment.Equals(_originatingEnvironmentDeployment))
                return;

            MasterEnvironmentLoadBalancer = _discovery.CreateLoadBalancer(_masterDeployment, _reachabilityCheck, TrafficRoutingStrategy.RandomByRequestID);
        }

        private void CreateOriginatingEnvironmentLoadBalancer()
        {            
            OriginatingEnvironmentLoadBalancer = _discovery.CreateLoadBalancer(_originatingEnvironmentDeployment, _reachabilityCheck, TrafficRoutingStrategy.RandomByRequestID);
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
            MasterEnvironmentLoadBalancer.Dispose();
            OriginatingEnvironmentLoadBalancer.Dispose();
            _configBlockLink?.Dispose();
            _healthCheck.Dispose();
        }
    }


}