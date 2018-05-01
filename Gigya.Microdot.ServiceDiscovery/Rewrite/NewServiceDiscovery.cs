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

        private ILoadBalancer MasterEnvironmentLoadBalancer { get; set; }
        private readonly DeploymentIdentifier _masterDeployment;

        private ILoadBalancer OriginatingEnvironmentLoadBalancer { get; set; }

        private ILog Log { get; }
        private readonly IDiscoveryFactory _discoveryFactory;
        private Func<DiscoveryConfig> GetConfig { get; }

        private readonly DeploymentIdentifier _originatingDeployment;

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private bool _disposed = false;
        private readonly ReachabilityCheck _reachabilityCheck;
        private readonly object _locker = new object();
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
            _originatingDeployment = new DeploymentIdentifier(serviceName, environmentVariableProvider.DeploymentEnvironment);
            _masterDeployment = new DeploymentIdentifier(serviceName, MASTER_ENVIRONMENT);

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
            var newServiceConfig = discoveryConfig.Services[_serviceName];

            lock (_locker)
            {
                if (newServiceConfig.Equals(LastServiceConfig) &&
                    discoveryConfig.EnvironmentFallbackEnabled == LastConfig.EnvironmentFallbackEnabled)
                    return;
            }

            lock (_locker)
            {
                LastConfig = discoveryConfig;
                LastServiceConfig = newServiceConfig;
            }
            await ReloadOriginatingEnvironmentLoadBalancer().ConfigureAwait(false);
            await ReloadMasterEnvironmentLoadBalancer().ConfigureAwait(false);            
        }

        private async Task ReloadMasterEnvironmentLoadBalancer()
        {
            var newLoadBalancer = await _discoveryFactory.TryCreateLoadBalancer(_masterDeployment, _reachabilityCheck).ConfigureAwait(false);
            RemoveMasterPool();
            MasterEnvironmentLoadBalancer = newLoadBalancer;
        }

        private async Task ReloadOriginatingEnvironmentLoadBalancer()
        {
            var newLoadBalancer = await _discoveryFactory.TryCreateLoadBalancer(_originatingDeployment, _reachabilityCheck).ConfigureAwait(false);
            lock (_locker)
            {
                RemoveOriginatingPool();
                OriginatingEnvironmentLoadBalancer = newLoadBalancer;
            }
        }

        private void RemoveOriginatingPool()
        {
            OriginatingEnvironmentLoadBalancer?.Dispose();
            OriginatingEnvironmentLoadBalancer = null;
        }

        private void RemoveMasterPool()
        {
            MasterEnvironmentLoadBalancer?.Dispose();
            MasterEnvironmentLoadBalancer = null;
        }

        private async Task<ILoadBalancer> GetRelevantLoadBalancer()
        {
            if (MasterEnvironmentLoadBalancer?.WasUndeployed != false)
                await ReloadMasterEnvironmentLoadBalancer().ConfigureAwait(false);

            if (OriginatingEnvironmentLoadBalancer?.WasUndeployed != false)
                await ReloadOriginatingEnvironmentLoadBalancer().ConfigureAwait(false);

            if (MasterEnvironmentLoadBalancer?.WasUndeployed == false &&
                OriginatingEnvironmentLoadBalancer?.WasUndeployed != false)
            {
                return MasterEnvironmentLoadBalancer;
            }
            else
            {
                return OriginatingEnvironmentLoadBalancer;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            RemoveMasterPool();
            RemoveOriginatingPool();
            _configBlockLink?.Dispose();
        }
    }


}