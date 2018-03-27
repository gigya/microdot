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
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// This class is an adapter between old interface if IServiceDiscovery and new Discovery implementation 
    /// TODO: Delete this class after Discovery Rewrite is completed.
    /// </summary>
    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    public sealed class NewServiceDiscovery : IServiceDiscovery, IDisposable
    {        
        public ISourceBlock<string> EndPointsChanged => _endPointsChanged;
        public ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged => _reachabilityChanged;

        internal DiscoveryConfig LastConfig { get; private set; }
        internal ServiceDiscoveryConfig LastServiceConfig { get; private set; }

        private LoadBalancer MasterEnvironmentLoadBalancer { get; set; }
        private List<IDisposable> _masterEnvironmentLinks = new List<IDisposable>();
        private readonly ServiceDeployment _masterDeployment;
        private readonly INode[] NoNodes = new INode[0];

        private LoadBalancer OriginatingEnvironmentLoadBalancer { get; set; }

        private INodeSource _originatingSource;
        private INodeSource _masterSource;
        private ILog Log { get; }
        private Func<DiscoveryConfig> GetConfig { get; }

        private List<IDisposable> _originatingEnvironmentLinks = new List<IDisposable>();
        private readonly ServiceDeployment _originatingDeployment;

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private bool _disposed = false;
        private readonly ReachabilityChecker _reachabilityChecker;
        private readonly ILoadBalancerFactory _loadBalancerFactory;
        private readonly INodeSourceLoader _nodeLoader;
        private readonly object _locker = new object();
        private readonly IDisposable _configBlockLink;
        private readonly BroadcastBlock<string> _endPointsChanged = new BroadcastBlock<string>(null);
        private readonly Task _initTask;
        private readonly BroadcastBlock<ServiceReachabilityStatus> _reachabilityChanged = new BroadcastBlock<ServiceReachabilityStatus>(null);
        private bool _firstTime = true;
        private volatile bool _suppressNotifications;
        private LoadBalancer _activeLoadBalancer;
        private INode[] _lastKnownNodes;
        private ILoadBalancer _lastKnownActiveLoadBalancer;
        private bool _lastKnownSourceWasUndeployed;


        public NewServiceDiscovery(string serviceName,
                                ReachabilityChecker reachabilityChecker,
                                ILoadBalancerFactory loadBalancerFactory,
                                INodeSourceLoader nodeLoader,
                                IEnvironmentVariableProvider environmentVariableProvider,
                                ISourceBlock<DiscoveryConfig> configListener,
                                Func<DiscoveryConfig> discoveryConfigFactory,
                                ILog log)
        {
            Log = log;
            _serviceName = serviceName;
            _originatingDeployment = new ServiceDeployment(serviceName, environmentVariableProvider.DeploymentEnvironment);
            _masterDeployment = new ServiceDeployment(serviceName, MASTER_ENVIRONMENT);

            _reachabilityChecker = reachabilityChecker;
            _loadBalancerFactory = loadBalancerFactory;
            _nodeLoader = nodeLoader;
            GetConfig = discoveryConfigFactory;
            // Must be run in Task.Run() because of incorrect Orleans scheduling
            _initTask = Task.Run(() => ReloadRemoteHost(discoveryConfigFactory()));
            Task.Run(CheckForChangesLoop);
            _configBlockLink = configListener.LinkTo(new ActionBlock<DiscoveryConfig>(ReloadRemoteHost));            
        }        

        public async Task<EndPoint[]> GetAllEndPoints()
        {
            await _initTask.ConfigureAwait(false);
            return GetRelevantLoadBalancer().NodeSource.GetNodes()
                .Select(n=>new EndPoint{HostName = n.Hostname, Port = n.Port})
                .ToArray();
        }

        public async Task<IEndPointHandle> GetNextHost(string affinityToken = null)
        {
            await _initTask.ConfigureAwait(false);

            return TryGetHostOverride() ?? new MonitoredNodeHandle(GetRelevantLoadBalancer().GetNode());
        }


        public async Task<IEndPointHandle> GetOrWaitForNextHost(CancellationToken cancellationToken)
        {
            await _initTask.ConfigureAwait(false);
            return TryGetHostOverride() ?? new MonitoredNodeHandle(GetRelevantLoadBalancer().GetNode());
        }

        private IEndPointHandle TryGetHostOverride()
        {
            var hostOverride = TracingContext.GetHostOverride(_serviceName);
            if (hostOverride == null)
                return null;
            return new OverriddenRemoteHost(_serviceName, hostOverride.Hostname, hostOverride.Port ?? GetConfig().Services[_serviceName].DefaultPort);
            
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

            _originatingSource = await GetNodeSource(_originatingDeployment, newServiceConfig).ConfigureAwait(false);

            var shouldCreateMasterPool = newConfig.EnvironmentFallbackEnabled &&
                                         newServiceConfig.Scope == ServiceScope.Environment &&
                                         _originatingSource.SupportsMultipleEnvironments &&
                                         _originatingDeployment.Equals(_masterDeployment) == false;

            if (shouldCreateMasterPool)
                _masterSource = await GetNodeSource(_masterDeployment, newServiceConfig).ConfigureAwait(false);

            lock (_locker)
            {
                _suppressNotifications = true;

                LastConfig = newConfig;
                LastServiceConfig = newServiceConfig;

                RemoveOriginatingPool();
                OriginatingEnvironmentLoadBalancer = CreateLoadBalancer(_originatingDeployment, _originatingSource);

                RemoveMasterPool();

                if (_masterSource != null)
                    MasterEnvironmentLoadBalancer = CreateLoadBalancer(_masterDeployment, _masterSource);

                _suppressNotifications = false;

                GetRelevantLoadBalancer();
            }
        }

        private void RemoveOriginatingPool()
        {
            if (_activeLoadBalancer == OriginatingEnvironmentLoadBalancer) _activeLoadBalancer = null;

            OriginatingEnvironmentLoadBalancer?.Dispose();
            OriginatingEnvironmentLoadBalancer = null;
            _originatingEnvironmentLinks.ForEach(x => x?.Dispose());
            _originatingEnvironmentLinks = new List<IDisposable>();
        }

        private void RemoveMasterPool()
        {
            if (_activeLoadBalancer == MasterEnvironmentLoadBalancer) _activeLoadBalancer = null;

            MasterEnvironmentLoadBalancer?.Dispose();
            MasterEnvironmentLoadBalancer = null;
            _masterEnvironmentLinks.ForEach(x => x?.Dispose());
            _masterEnvironmentLinks = new List<IDisposable>();
        }

        private LoadBalancer CreateLoadBalancer(
            ServiceDeployment serviceDeployment,            
            INodeSource nodeSource)
        {
            var reachabilityCheck = new ReachabilityCheck(n=>_reachabilityChecker(new NodeHandle(n)));
            return (LoadBalancer)_loadBalancerFactory.Create(nodeSource, serviceDeployment, reachabilityCheck);
        }

        private async Task CheckForChangesLoop()
        {
            while (!_disposed)
            {
                var loadBalancer = GetRelevantLoadBalancer();
                if (loadBalancer != null)
                {
                    var source = loadBalancer.NodeSource;
                    var nodes = NoNodes;
                    try
                    {
                        if (!source.WasUndeployed)
                            nodes = source.GetNodes();
                    }
                    catch
                    {
                    }

                    var wasUndeployed = source.WasUndeployed;
                    if (nodes != _lastKnownNodes)
                    {
                        FireEndPointChange();
                    }
                    if (_lastKnownActiveLoadBalancer != _activeLoadBalancer ||
                        wasUndeployed != _lastKnownSourceWasUndeployed)
                    {
                        _reachabilityChanged.Post(new ServiceReachabilityStatus {IsReachable = !source.WasUndeployed});
                    }
                    _lastKnownActiveLoadBalancer = _activeLoadBalancer;
                    _lastKnownNodes = nodes;
                    _lastKnownSourceWasUndeployed = wasUndeployed;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(500));

            }
        }


        private async Task<INodeSource> GetNodeSource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig config)
        {
            var source = new PersistentNodeSource(()=>_nodeLoader.GetNodeSource(serviceDeployment, config));

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
                    FireEndPointChange();
                }

                return _activeLoadBalancer;
            }
        }

        private string _endPointsSignature;        

        private void FireEndPointChange()
        {
            lock (_locker)
            {
                var relevantPool = GetRelevantLoadBalancer();
                IEnumerable<INode> nodes = new INode[0];
                try
                {
                    if (!relevantPool.NodeSource.WasUndeployed)
                        nodes = relevantPool.NodeSource.GetNodes().OrderBy(x => x.Hostname).ThenBy(x => x.Port ?? 0);
                }
                catch
                {
                }
                string newSignature = string.Join(",", nodes.Select(x =>
                    {
                        var port = x.Port.HasValue ? $":{x.Port}" : string.Empty;
                        return $"{x.Hostname}{port}";
                    }));



                if (!_firstTime && _endPointsSignature != newSignature && !_suppressNotifications)
                {
                    // Don't send any information on the event.
                    // Any information should be collected from current state                     
                    _endPointsChanged.Post(null);
                }
                _firstTime = false;
                _endPointsSignature = newSignature;

            }

        }

        public void Dispose()
        {
            _disposed = true;
            RemoveMasterPool();
            RemoveOriginatingPool();
            _endPointsChanged.Complete();
            _reachabilityChanged.Complete();
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
        private bool _isUndeployed;
        private Task _redeployCheck;
        private object _locker = new object();
        private bool _disposed = false;

        private Func<INodeSource> CreateNewSource { get; }


        public PersistentNodeSource(Func<INodeSource> createNewSource)
        {
            CreateNewSource = createNewSource;
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
                var wasUndeployed = _currentSource.WasUndeployed;
                if (wasUndeployed)
                {
                    CheckIfRedeployed();
                }
                return _isUndeployed;
            }
        }

        private void CheckIfRedeployed()
        {
            lock (_locker)
            {
                if (_currentSource.WasUndeployed)
                {
                    _isUndeployed = true;
                    if (_redeployCheck == null || _redeployCheck.IsCompleted)
                    {
                        _redeployCheck = RecreateSourceToCheckIfItWasRedeployed();
                    }
                }
            }            
        }

        private async Task RecreateSourceToCheckIfItWasRedeployed()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
                
            var newSource = CreateNewSource();
            await newSource.Init();

            lock (_locker)
            {
                if (_disposed)
                    newSource.Dispose();
                else
                {
                    var oldSource = _currentSource;
                    _currentSource = newSource;
                    oldSource.Dispose();
                    _isUndeployed = _currentSource.WasUndeployed;
                }
            }
        }

        public bool SupportsMultipleEnvironments => _currentSource.SupportsMultipleEnvironments;
    }

    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    public interface ILoadBalancerFactory
    {
        ILoadBalancer Create(INodeSource nodeSource, ServiceDeployment serviceDeployment,
            ReachabilityCheck reachabilityChecker);
    }

    /// <summary>
    /// This class is an adapter between old interface IEndpointHandle and new implementation of MonitoredNode.
    /// TODO: Delete this class after Discovery Rewrite is completed.
    /// </summary>
    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    internal class MonitoredNodeHandle : IEndPointHandle
    {
        private readonly MonitoredNode _monitoredNode;

        public MonitoredNodeHandle(MonitoredNode monitoredNode)
        {
            _monitoredNode = monitoredNode;            
        }

        public string HostName => _monitoredNode.Hostname;
        public int? Port => _monitoredNode.Port;
        public bool ReportFailure(Exception ex = null)
        {
            _monitoredNode.ReportUnreachable(ex);
            return true;
        }

        public void ReportSuccess()
        {
            _monitoredNode.ReportReachable();
        }
    }

    /// <summary>
    /// This class is an adapter between old interface IEndpointHandle and new implementation of INode.
    /// TODO: Delete this class after Discovery Rewrite is completed.
    /// </summary>
    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    internal class NodeHandle : IEndPointHandle
    {
        private readonly INode _node;

        public NodeHandle(INode node)
        {
            _node = node;
        }

        public string HostName => _node.Hostname;
        public int? Port => _node.Port;
        public bool ReportFailure(Exception ex = null)
        {            
            // do nothing
            return true;
        }

        public void ReportSuccess()
        {
            // do nothing
        }
    }

}