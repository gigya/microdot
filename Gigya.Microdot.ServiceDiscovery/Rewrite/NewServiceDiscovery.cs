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
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Rewrite;
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
        private readonly DeploymentIdentifier _noEnvironmentDeployment;
        private ILoadBalancer MasterEnvironmentLoadBalancer { get; set; }
        private ILoadBalancer OriginatingEnvironmentLoadBalancer { get; set; }
        private ILoadBalancer NoEnvironmentLoadBalancer { get; set; }

        private ILog Log { get; }
        private readonly IDiscoveryFactory _discoveryFactory;
        private Func<DiscoveryConfig> GetConfig { get; }


        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private bool _disposed = false;
        private readonly ReachabilityCheck _reachabilityCheck;
        private readonly object _locker = new object();
        private readonly AsyncLock _asyncLocker = new AsyncLock();
        private readonly IDisposable _configBlockLink;
        private readonly Task _initTask;

        public NewServiceDiscovery(string serviceName,
                                ReachabilityCheck reachabilityCheck,
                                IEnvironmentVariableProvider environmentVariableProvider,
                                ISourceBlock<DiscoveryConfig> configListener,
                                Func<DiscoveryConfig> discoveryConfigFactory,
                                ILog log,
                                IDiscoveryFactory discoveryFactory)
        {
            Log = log;
            _discoveryFactory = discoveryFactory;            
            _serviceName = serviceName;
            
            _originatingEnvironmentDeployment = new DeploymentIdentifier(serviceName, environmentVariableProvider.DeploymentEnvironment);
            _masterDeployment = new DeploymentIdentifier(serviceName, MASTER_ENVIRONMENT);
            _noEnvironmentDeployment = new DeploymentIdentifier(serviceName);

            _reachabilityCheck = reachabilityCheck;
            GetConfig = discoveryConfigFactory;
            // Must be run in Task.Run() because of incorrect Orleans scheduling
            _initTask = Task.Run(() => ReloadRemoteHost(discoveryConfigFactory()));
            _configBlockLink = configListener.LinkTo(new ActionBlock<DiscoveryConfig>(ReloadRemoteHost));
        }



        public async Task<IMonitoredNode> GetNode()
        {
            await _initTask.ConfigureAwait(false);
            
            IMonitoredNode node = TryGetHostOverride();
            if (node != null)
                return node;

            ILoadBalancer relevantLoadBalancer = await GetRelevantLoadBalancer();
            if (relevantLoadBalancer==null)            
                throw new ServiceUnreachableException("Service is not deployed");

            return relevantLoadBalancer.GetNode();
        }

        private IMonitoredNode TryGetHostOverride()
        {
            var hostOverride = TracingContext.GetHostOverride(_serviceName);
            if (hostOverride == null)
                return null;

            return new OverriddenNode(_serviceName, hostOverride.Hostname, hostOverride.Port ?? GetConfig().Services[_serviceName].DefaultPort);            
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

                await ReloadOriginatingEnvironmentLoadBalancer().ConfigureAwait(false);
                await ReloadMasterEnvironmentLoadBalancer().ConfigureAwait(false);
                await ReloadNoEnvironmentLoadBalancer().ConfigureAwait(false);
            }
        }

        private async Task ReloadMasterEnvironmentLoadBalancer()
        {
            RemoveMasterPool();
            if (_masterDeployment.Equals(_originatingEnvironmentDeployment))
                return;

            MasterEnvironmentLoadBalancer = await _discoveryFactory.TryCreateLoadBalancer(_masterDeployment, _reachabilityCheck).ConfigureAwait(false);
        }

        private async Task ReloadOriginatingEnvironmentLoadBalancer()
        {            
            RemoveOriginatingPool();
            OriginatingEnvironmentLoadBalancer = await _discoveryFactory.TryCreateLoadBalancer(_originatingEnvironmentDeployment, _reachabilityCheck).ConfigureAwait(false);
        }

        private async Task ReloadNoEnvironmentLoadBalancer()
        {
            RemoveNoEnvironmentPool();
            NoEnvironmentLoadBalancer = await _discoveryFactory.TryCreateLoadBalancer(_noEnvironmentDeployment, _reachabilityCheck).ConfigureAwait(false);            
        }

        private void RemoveOriginatingPool()
        {
            OriginatingEnvironmentLoadBalancer?.DisposeAsync();
            OriginatingEnvironmentLoadBalancer = null;
        }

        private void RemoveMasterPool()
        {
            MasterEnvironmentLoadBalancer?.DisposeAsync();
            MasterEnvironmentLoadBalancer = null;
        }

        private void RemoveNoEnvironmentPool()
        {
            NoEnvironmentLoadBalancer?.DisposeAsync();
            NoEnvironmentLoadBalancer = null;
        }

        private async Task<ILoadBalancer> GetRelevantLoadBalancer()
        {
            var config = GetConfig();
            if (config != LastConfig)
                await ReloadRemoteHost(config).ConfigureAwait(false);

            using (await _asyncLocker.LockAsync().ConfigureAwait(false))
            {
                if (MasterEnvironmentLoadBalancer?.NodeSource?.WasUndeployed != false)
                    await ReloadMasterEnvironmentLoadBalancer().ConfigureAwait(false);

                if (OriginatingEnvironmentLoadBalancer?.NodeSource?.WasUndeployed != false)
                    await ReloadOriginatingEnvironmentLoadBalancer().ConfigureAwait(false);

                if (NoEnvironmentLoadBalancer?.NodeSource?.WasUndeployed != false)
                    await ReloadNoEnvironmentLoadBalancer().ConfigureAwait(false);

                if (OriginatingEnvironmentLoadBalancer?.NodeSource?.WasUndeployed == false)
                {
                    return OriginatingEnvironmentLoadBalancer;
                }
                else if (MasterEnvironmentLoadBalancer?.NodeSource?.WasUndeployed == false && GetConfig().EnvironmentFallbackEnabled)
                {
                    return MasterEnvironmentLoadBalancer;
                }
                else if (NoEnvironmentLoadBalancer?.NodeSource?.WasUndeployed == false)
                {
                    return NoEnvironmentLoadBalancer;
                }
                else
                    return null;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            RemoveMasterPool();
            RemoveOriginatingPool();
            RemoveNoEnvironmentPool();
            _configBlockLink?.Dispose();
        }
    }


}