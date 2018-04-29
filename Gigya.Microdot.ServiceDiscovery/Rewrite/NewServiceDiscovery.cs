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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Rewrite;
using Nito.AsyncEx.Synchronous;

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

        private LoadBalancer MasterEnvironmentLoadBalancer { get; set; }
        private readonly DeploymentIdentifier _masterDeployment;

        private LoadBalancer OriginatingEnvironmentLoadBalancer { get; set; }

        private INodeSource _originatingSource;
        private INodeSource _masterSource;
        private ILog Log { get; }
        private readonly IServiceListMonitor _serviceListMonitor;
        private Func<DiscoveryConfig> GetConfig { get; }

        private readonly DeploymentIdentifier _originatingDeployment;

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private bool _disposed = false;
        private readonly ReachabilityCheck _reachabilityCheck;
        private readonly ILoadBalancerFactory _loadBalancerFactory;
        private readonly INodeSourceLoader _nodeLoader;
        private readonly object _locker = new object();
        private readonly IDisposable _configBlockLink;
        private readonly Task _initTask;
        private LoadBalancer _activeLoadBalancer;


        public NewServiceDiscovery(string serviceName,
                                ReachabilityCheck reachabilityCheck,
                                ILoadBalancerFactory loadBalancerFactory,
                                INodeSourceLoader nodeLoader,
                                IEnvironmentVariableProvider environmentVariableProvider,
                                ISourceBlock<DiscoveryConfig> configListener,
                                Func<DiscoveryConfig> discoveryConfigFactory,
                                ILog log,
                                IServiceListMonitor serviceListMonitor)
        {
            Log = log;
            _serviceListMonitor = serviceListMonitor;
            _dateTime = dateTime;
            _serviceName = serviceName;
            _originatingDeployment = new DeploymentIdentifier(serviceName, environmentVariableProvider.DeploymentEnvironment);
            _masterDeployment = new DeploymentIdentifier(serviceName, MASTER_ENVIRONMENT);

            _reachabilityCheck = reachabilityCheck;
            _loadBalancerFactory = loadBalancerFactory;
            _nodeLoader = nodeLoader;
            GetConfig = discoveryConfigFactory;
            // Must be run in Task.Run() because of incorrect Orleans scheduling
            _initTask = Task.Run(() => ReloadRemoteHost(discoveryConfigFactory()));
            _configBlockLink = configListener.LinkTo(new ActionBlock<DiscoveryConfig>(ReloadRemoteHost));            
        }        

  

        public async Task<IMonitoredNode> GetNode()
        {
            await _initTask.ConfigureAwait(false);

            return TryGetHostOverride() ?? GetRelevantLoadBalancer().GetNode();
        }

        private IMonitoredNode TryGetHostOverride()
        {
            var hostOverride = TracingContext.GetHostOverride(_serviceName);
            if (hostOverride == null)
                return null;

            return new OverriddenNode(_serviceName, hostOverride.Hostname, hostOverride.Port ?? GetConfig().Services[_serviceName].DefaultPort);            
        }

        private async Task ReloadRemoteHost(DiscoveryConfig newConfig)
        {
            var newServiceConfig = newConfig.Services[_serviceName];

            lock (_locker)
            {
                if (newServiceConfig.Equals(LastServiceConfig) &&
                    newConfig.EnvironmentFallbackEnabled == LastConfig.EnvironmentFallbackEnabled)
                    return;
            }

            _originatingSource = await CreateNodeSource(_originatingDeployment, newServiceConfig).ConfigureAwait(false);

            var shouldCreateMasterPool = newConfig.EnvironmentFallbackEnabled &&
                                         newServiceConfig.Scope == ServiceScope.Environment &&
                                         _originatingSource.SupportsMultipleEnvironments &&
                                         _originatingDeployment.Equals(_masterDeployment) == false;

            if (shouldCreateMasterPool)
                _masterSource = await CreateNodeSource(_masterDeployment, newServiceConfig).ConfigureAwait(false);

            lock (_locker)
            {
                
                LastConfig = newConfig;
                LastServiceConfig = newServiceConfig;

                RemoveOriginatingPool();
                OriginatingEnvironmentLoadBalancer = CreateLoadBalancer(_originatingDeployment, _originatingSource);

                RemoveMasterPool();

                if (_masterSource != null)
                    MasterEnvironmentLoadBalancer = CreateLoadBalancer(_masterDeployment, _masterSource);
            }
        }

        private void RemoveOriginatingPool()
        {
            if (_activeLoadBalancer == OriginatingEnvironmentLoadBalancer) _activeLoadBalancer = null;

            OriginatingEnvironmentLoadBalancer?.Dispose();
            OriginatingEnvironmentLoadBalancer = null;
        }

        private void RemoveMasterPool()
        {
            if (_activeLoadBalancer == MasterEnvironmentLoadBalancer) _activeLoadBalancer = null;

            MasterEnvironmentLoadBalancer?.Dispose();
            MasterEnvironmentLoadBalancer = null;
        }

        private LoadBalancer CreateLoadBalancer(
            DeploymentIdentifier deploymentIdentifier,            
            INodeSource nodeSource)
        {
            return (LoadBalancer)_loadBalancerFactory.Create(nodeSource, deploymentIdentifier, _reachabilityCheck);
        }

        private async Task<INodeSource> CreateNodeSource(DeploymentIdentifier deploymentIdentifier, ServiceDiscoveryConfig config)
        {
            var source = new PersistentNodeSource(deploymentIdentifier.ToString(), ()=>_nodeLoader.GetNodeSource(deploymentIndentifier, config), _serviceListMonitor);

            await source.Init().ConfigureAwait(false);

            GetRelevantLoadBalancer();

            return source;

        }

        private LoadBalancer GetRelevantLoadBalancer()
        {
            lock (_locker)
            {
                bool shouldFallBack = MasterEnvironmentLoadBalancer != null
                                      && _originatingSource?.WasUndeployed==true;

                LoadBalancer newActiveLoadBalancer = shouldFallBack ? MasterEnvironmentLoadBalancer : OriginatingEnvironmentLoadBalancer;

                if (newActiveLoadBalancer != _activeLoadBalancer)
                {
                    Log.Info(x=>x("Discovery host pool has changed", unencryptedTags: new {serviceName = _serviceName, previousPool = _activeLoadBalancer?.ServiceName, newPool = newActiveLoadBalancer.ServiceName}));
                    _activeLoadBalancer = newActiveLoadBalancer;
                }

                return _activeLoadBalancer;
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

    /// <summary>
    /// This class is an adapter for nodesSource which enables working with a node source event when it was undeployed,
    /// without need to dispose it an re-construct it when node is deployed again.
    /// TODO: Delete this class after Discovery Rewrite is completed.
    /// </summary>
    public class PersistentNodeSource : INodeSource
    {
        private INodeSource _currentSource;
        private Task _redeployCheck;
        private readonly object _locker = new object();
        private bool _disposed = false;

        private int _lastServiceListVersion = 0;
        public string DeploymentIdentifier { get; }
        private Func<INodeSource> CreateNewSource { get; }
        private IServiceListMonitor ServiceListMonitor { get; }


        public PersistentNodeSource(string deploymentIdentifier, Func<INodeSource> createNewSource, IServiceListMonitor serviceListMonitor)
        {
            DeploymentIdentifier = deploymentIdentifier;
            CreateNewSource = createNewSource;
            ServiceListMonitor = serviceListMonitor;
            _currentSource = CreateNewSource();
        }

        public void Dispose()
        {
            lock (_locker)
            {
                _disposed = true;
                _currentSource.Dispose();
            }
        }

        public Task Init()
        {
            return _currentSource.Init();
        }

        public string Type => _currentSource.Type;
        public INode[] GetNodes()
        {
            return WasUndeployed ? new INode[0] : _currentSource.GetNodes();
        }

        public bool WasUndeployed
        {
            get
            {                
                if (_currentSource.WasUndeployed)
                {
                    return CheckIfStillUndeployed();
                }
                return false;
            }
        }

        private bool CheckIfStillUndeployed()
        {
            var isStillUndeployed = true;
            lock (_locker)
            {
                if (_lastServiceListVersion != ServiceListMonitor.Version)
                {
                    if (ServiceListMonitor.Services.Contains(DeploymentIdentifier))
                    {
                        isStillUndeployed = false;
                        if (_redeployCheck == null || _redeployCheck.IsCompleted)
                            _redeployCheck = RecreateSourceToCheckIfItWasRedeployed();//Ignore ex 
                    }
                    _lastServiceListVersion = ServiceListMonitor.Version;
                }
            }
            return isStillUndeployed;
        }

        private async Task RecreateSourceToCheckIfItWasRedeployed()
        {
            if (_disposed) return;

            var newSource = CreateNewSource();
            await newSource.Init().ConfigureAwait(false);

            lock (_locker)
            {
                if (_disposed)
                    newSource.Dispose();
                else
                {
                    var oldSource = _currentSource;
                    _currentSource = newSource;
                    oldSource.Dispose();
                }
            }
        }

        public bool SupportsMultipleEnvironments => _currentSource.SupportsMultipleEnvironments;
    }

    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    public interface ILoadBalancerFactory
    {
        ILoadBalancer Create(INodeSource nodeSource, DeploymentIdentifier deploymentIdentifier,
            ReachabilityCheck reachabilityChecker);
    }


}