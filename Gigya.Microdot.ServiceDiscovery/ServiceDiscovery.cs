﻿#region Copyright 
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
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceDiscovery
{
    public sealed class ServiceDiscovery : IServiceDiscovery, IDisposable
    {
        public ISourceBlock<string> EndPointsChanged => _endPointsChanged;
        public ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged => _reachabilityChanged;

        internal DiscoveryConfig LastConfig { get; private set; }
        internal ServiceDiscoveryConfig LastServiceConfig { get; private set; }

        private RemoteHostPool MasterEnvironmentPool { get; set; }
        private List<IDisposable> _masterEnvironmentLinks = new List<IDisposable>();
        private readonly ServiceDeployment _masterDeployment;

        private RemoteHostPool OriginatingEnvironmentPool { get; set; }
        private List<IDisposable> _originatingEnvironmentLinks = new List<IDisposable>();
        private readonly ServiceDeployment _originatingDeployment;

        private const string MASTER_ENVIRONMENT = "prod";
        private readonly string _serviceName;
        private readonly ReachabilityChecker _reachabilityChecker;
        private readonly IRemoteHostPoolFactory _remoteHostPoolFactory;
        private readonly IDiscoverySourceLoader _discoverySourceLoader;
        private readonly object _locker = new object();
        private readonly IDisposable _configBlockLink;
        private readonly BroadcastBlock<string> _endPointsChanged = new BroadcastBlock<string>(null);
        private readonly Task _initTask;
        private readonly BroadcastBlock<ServiceReachabilityStatus> _reachabilityChanged = new BroadcastBlock<ServiceReachabilityStatus>(null);
        private bool _firstTime = true;
        private volatile bool _suppressNotifications;
        private RemoteHostPool _activePool;

        public ServiceDiscovery(string serviceName,
                                ReachabilityChecker reachabilityChecker,
                                IRemoteHostPoolFactory remoteHostPoolFactory,
                                IDiscoverySourceLoader discoverySourceLoader,
                                IEnvironmentVariableProvider environmentVariableProvider,
                                ISourceBlock<DiscoveryConfig> configListener,
                                Func<DiscoveryConfig> discoveryConfigFactory)
        {
            _serviceName = serviceName;
            _originatingDeployment = new ServiceDeployment(serviceName, environmentVariableProvider.DeploymentEnvironment);
            _masterDeployment = new ServiceDeployment(serviceName, MASTER_ENVIRONMENT);

            _reachabilityChecker = reachabilityChecker;
            _remoteHostPoolFactory = remoteHostPoolFactory;
            _discoverySourceLoader = discoverySourceLoader;

            // Must be run in Task.Run() because of incorrect Orleans scheduling
            _initTask = Task.Run(() => ReloadRemoteHost(discoveryConfigFactory()));
            _configBlockLink = configListener.LinkTo(new ActionBlock<DiscoveryConfig>(ReloadRemoteHost));

        }

        public async Task<EndPoint[]> GetAllEndPoints()
        {
            await _initTask.ConfigureAwait(false);
            return GetRelevantPool().GetAllEndPoints();
        }

        public async Task<IEndPointHandle> GetNextHost(string affinityToken = null)
        {
            await _initTask.ConfigureAwait(false);
            return GetRelevantPool().GetNextHost(affinityToken);
        }


        public async Task<IEndPointHandle> GetOrWaitForNextHost(CancellationToken cancellationToken)
        {
            await _initTask.ConfigureAwait(false);
            return await GetRelevantPool().GetOrWaitForNextHost(cancellationToken).ConfigureAwait(false);
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

            var shouldCreateMasterPool = newConfig.EnvironmentFallbackEnabled &&
                                         newServiceConfig.SupportsFallback &&
                                         _originatingDeployment.Equals(_masterDeployment) == false;

            ServiceDiscoverySourceBase masterSource = null;

            var originatingSource = await GetServiceDiscoverySource(_originatingDeployment, newServiceConfig).ConfigureAwait(false);

            if (shouldCreateMasterPool)
                masterSource = await GetServiceDiscoverySource(_masterDeployment, newServiceConfig).ConfigureAwait(false);

            lock (_locker)
            {
                _suppressNotifications = true;

                LastConfig = newConfig;
                LastServiceConfig = newServiceConfig;

                RemoveOriginatingPool();
                OriginatingEnvironmentPool = CreatePool(_originatingDeployment, _originatingEnvironmentLinks, originatingSource);

                RemoveMasterPool();

                if (masterSource != null)
                    MasterEnvironmentPool = CreatePool(_masterDeployment, _masterEnvironmentLinks, masterSource);

                _suppressNotifications = false;

                GetRelevantPool();
            }
        }

        private void RemoveOriginatingPool()
        {
            if (_activePool == OriginatingEnvironmentPool) _activePool = null;

            OriginatingEnvironmentPool?.Dispose();
            OriginatingEnvironmentPool = null;
            _originatingEnvironmentLinks.ForEach(x => x?.Dispose());
            _originatingEnvironmentLinks = new List<IDisposable>();
        }

        private void RemoveMasterPool()
        {
            if (_activePool == MasterEnvironmentPool) _activePool = null;

            MasterEnvironmentPool?.Dispose();
            MasterEnvironmentPool = null;
            _masterEnvironmentLinks.ForEach(x => x?.Dispose());
            _masterEnvironmentLinks = new List<IDisposable>();
        }

        private RemoteHostPool CreatePool(
            ServiceDeployment serviceDeployment,
            List<IDisposable> blockLinks,
            ServiceDiscoverySourceBase discoverySource)
        {
            var result = _remoteHostPoolFactory.Create(serviceDeployment, discoverySource, _reachabilityChecker);

            var dispose = result.EndPointsChanged.LinkTo(new ActionBlock<EndPointsResult>(m =>
                {
                    if (result == MasterEnvironmentPool || result == OriginatingEnvironmentPool)
                    {
                        FireEndPointChange();
                    }
                }));

            blockLinks.Add(dispose);

            dispose = result.ReachabilitySource.LinkTo(new ActionBlock<ServiceReachabilityStatus>(x =>
                {
                    if (result == _activePool && !_suppressNotifications)
                        _reachabilityChanged.Post(x);
                }));
            blockLinks.Add(dispose);




            return result;
        }


        private async Task<ServiceDiscoverySourceBase> GetServiceDiscoverySource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig config)
        {
            var discoverySource = _discoverySourceLoader.GetDiscoverySource(serviceDeployment, config);

            // TODO: RemoteHostPool should either deal with uninitialized source or request different class which represents initialized source
            await discoverySource.InitCompleted.ConfigureAwait(false);
            return discoverySource;
        }


        private RemoteHostPool GetRelevantPool()
        {
            lock (_locker)
            {
                bool shouldFallBack = MasterEnvironmentPool != null
                                      && !OriginatingEnvironmentPool.IsServiceDeploymentDefined;

                RemoteHostPool newActivePool = shouldFallBack ? MasterEnvironmentPool : OriginatingEnvironmentPool;

                if (newActivePool != _activePool)
                {
                    _activePool = newActivePool;

                    FireEndPointChange();

                    if (shouldFallBack)
                        OriginatingEnvironmentPool.DeactivateMetrics();
                    else
                        MasterEnvironmentPool?.DeactivateMetrics();
                }

                return _activePool;
            }
        }

        private string _endPointsSignature;
        private void FireEndPointChange()
        {
            lock (_locker)
            {
                var relevantPool = GetRelevantPool();
                var endPoints = relevantPool.GetAllEndPoints().OrderBy(x => x.HostName).ThenBy(x=>x.Port??0);
                string newSignature = string.Join(",", endPoints.Select(x =>
                    {
                        var port = x.Port.HasValue ? $":{x.Port}" : string.Empty;
                        return $"{x.HostName}{port}";
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
            RemoveMasterPool();
            RemoveOriginatingPool();
            _endPointsChanged.Complete();
            _reachabilityChanged.Complete();
            _configBlockLink?.Dispose();
        }
    }
}