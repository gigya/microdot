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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulDiscoverySource : ServiceDiscoverySourceBase
    {
        public override Task InitCompleted => _initialized;
        public override bool IsServiceDeploymentDefined => _isDeploymentDefined;

        private IConsulClient ConsulClient { get; }
        private CancellationTokenSource ShutdownToken { get; }
        private readonly ServiceDiscoveryConfig _config;
        private readonly IDateTime _dateTime;
        private readonly ILog _log;

        private readonly object _lastResultLocker = new object();
        private EndPointsResult _lastEndpointsResult;

        private EndPointsResult _lastVersionResult;

        private Task _initialized;
        private Task _initializeVersion;

        private Task _loadVersionTask;
        private Task _loadEndpointsTask;

        private bool _firstTime = true;

        private ulong _endpointsModifyIndex=0;
        private ulong _versionModifyIndex = 0;
        private string _activeVersion;
        private bool _isDeploymentDefined = true;
        private bool _shouldReportChanges = false;

        public ConsulDiscoverySource(ServiceDeployment serviceDeployment,
            ServiceDiscoveryConfig serviceDiscoveryConfig,
            IConsulClient consulClient,
            IDateTime dateTime, ILog log)
            : base(GetDeploymentName(serviceDeployment, serviceDiscoveryConfig))

        {
            ConsulClient = consulClient;
            _config = serviceDiscoveryConfig;
            _dateTime = dateTime;
            _log = log;
            ShutdownToken = new CancellationTokenSource();
            Task.Run(() => RefreshVersionForever(ShutdownToken.Token));
            Task.Run(() => RefreshEndpointsForever(ShutdownToken.Token));
            
            
            _initializeVersion = Task.Run(LoadVersion); // Must be run in Task.Run() because of incorrect Orleans scheduling
            _initialized = Task.Run(LoadEndpoints);  // Must be run in Task.Run() because of incorrect Orleans scheduling
        }

        private async Task RefreshVersionForever(CancellationToken shutdownToken)
        {
            while (shutdownToken.IsCancellationRequested == false)
            {
                try
                {
                    await LoadVersion();
                }
                catch (Exception e)
                {
                    _log.Critical("Failed to load service version from Consul", e);
                }

                var delay = TimeSpan.FromMilliseconds(100);
                if (_lastVersionResult.Error != null)
                    delay = _config.ErrorRetryInterval.Value;
                else if (!_lastVersionResult.IsQueryDefined)
                    delay = _config.UndefinedRetryInterval;

                await _dateTime.Delay(delay).ConfigureAwait(false);
            }
        }

        private async Task RefreshEndpointsForever(CancellationToken shutdownToken)
        {
            while (shutdownToken.IsCancellationRequested == false)
            {
                try
                {
                    await LoadEndpoints().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _log.Critical("Failed to load endpoints from Consul", e);
                }

                var delay = _lastEndpointsResult.Error != null ? _config.ErrorRetryInterval.Value : TimeSpan.FromMilliseconds(100);
                await _dateTime.Delay(delay).ConfigureAwait(false);
            }
        }

        private Task LoadVersion()
        {
            lock (_lastResultLocker)
                return _loadVersionTask ?? (_loadVersionTask = Task.Run(LoadVersionAsync).ContinueWith(t => _loadVersionTask = null));
        }

        private async Task LoadVersionAsync()
        {
            var newVersionResult = await ConsulClient.GetServiceVersion(DeploymentName, _versionModifyIndex, _config.ReloadTimeout).ConfigureAwait(false);
            lock (_lastResultLocker)
            {
                _lastVersionResult = newVersionResult;
                _versionModifyIndex = _lastVersionResult.ModifyIndex ?? 0;

                if (_lastVersionResult.Error != null)
                {
                    _log.Error(x => x("Error getting service version from Consul", exception: _lastVersionResult.Error,
                        unencryptedTags: new {serviceName = DeploymentName}));
                    if (_firstTime)
                    {
                        Result = _lastVersionResult;
                        _firstTime = false;
                    }

                    _initializeVersion = Task.FromResult(1);
                    return;
                }

                if (_lastVersionResult.IsQueryDefined == false)
                {
                    if (_isDeploymentDefined)
                    {
                        _log.Warn(x => x("Service has become undefined on Consul", unencryptedTags: new
                        {
                            serviceName = DeploymentName
                        }));
                        _isDeploymentDefined = false;
                        _activeVersion = null;
                        _shouldReportChanges = true;
                        ReportChanges();
                    }                    
                }
                else if (_isDeploymentDefined == false)
                {
                    _shouldReportChanges = true;
                    _isDeploymentDefined = true;
                }

                _initializeVersion = Task.FromResult(1);

                if (_lastVersionResult.ActiveVersion != null && _activeVersion != _lastVersionResult.ActiveVersion)
                {
                    _activeVersion = _lastVersionResult.ActiveVersion;
                    _shouldReportChanges = true;
                }

                LoadEndpointsAsync();
            }
        }

        private Task LoadEndpoints()
        {
            lock (_lastResultLocker)
                return _loadEndpointsTask ?? (_loadEndpointsTask = Task.Run(LoadEndpointsAsync).ContinueWith(t=>_loadEndpointsTask=null));
        }

        private async Task LoadEndpointsAsync()
        {
            await _initializeVersion.ConfigureAwait(false);
            if (_isDeploymentDefined == false || _activeVersion == null)
            {
                if (_lastEndpointsResult == null)
                    _lastEndpointsResult = new EndPointsResult
                    {
                        IsQueryDefined = _isDeploymentDefined,
                        ActiveVersion = _activeVersion,
                        Error = _lastVersionResult?.Error,
                        RequestLog = _lastVersionResult?.RequestLog,
                        ResponseLog = _lastVersionResult?.ResponseLog                        
                    };

                _initialized = Task.FromResult(1);                
                return;
            }

            var newConsulResult = await ConsulClient.GetHealthyEndpoints(DeploymentName, _endpointsModifyIndex, _config.ReloadTimeout).ConfigureAwait(false);
            _endpointsModifyIndex = newConsulResult.ModifyIndex ?? 0;

            newConsulResult.EndPoints = newConsulResult.EndPoints
                .Where(e=>(e as ConsulEndPoint)?.Version==_activeVersion)
                .OrderBy(x => x.HostName)
                .ThenBy(x => x.Port)
                .ToArray();

            lock (_lastResultLocker)
            {
                _lastEndpointsResult = newConsulResult;

                if (newConsulResult.Error != null)
                {
                    if (_firstTime)
                    {
                        Result = newConsulResult;
                        _firstTime = false;
                    }

                    _initialized = Task.FromResult(1);                    
                    return;
                }
                
                if (newConsulResult.EndPoints.SequenceEqual(Result.EndPoints) == false)
                {
                    Result = newConsulResult;

                    _log.Info(_ => _("Obtained new list endpoints for service from Consul", unencryptedTags: new
                    {
                        serviceName = DeploymentName,
                        endpoints = string.Join(", ", newConsulResult.EndPoints.Select(e => e.HostName + ':' + (e.Port?.ToString() ?? "")))
                    }));

                    _shouldReportChanges = true;
                }

                ReportChanges();

                _firstTime = false;

                _initialized = Task.FromResult(1);
            }
        }

        private void ReportChanges()
        {
            lock (_lastResultLocker)
            {
                if (!_shouldReportChanges)
                    return;

                if (_lastEndpointsResult==null)
                    _lastEndpointsResult = new EndPointsResult();

                _lastEndpointsResult.ActiveVersion = _activeVersion;
                _lastEndpointsResult.IsQueryDefined = _isDeploymentDefined;
                if (!_isDeploymentDefined)
                    _lastEndpointsResult.EndPoints = new EndPoint[0];

                if (!_firstTime)
                    EndPointsChanged?.Post(_lastEndpointsResult);

                _firstTime = false;
                _shouldReportChanges = false;
            }
        }

        public override Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts)
        {
            if (endPointsResult.EndPoints.Length == 0)
            {
                var tags = new Tags
                {
                    {"requestedService", DeploymentName},
                    {"consulAddress", ConsulClient?.ConsulAddress?.ToString()},
                    { "requestTime", endPointsResult.RequestDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                    { "requestLog", endPointsResult.RequestLog },
                    { "responseLog", endPointsResult.ResponseLog },
                    { "queryDefined", endPointsResult.IsQueryDefined.ToString() },
                    { "consulError", endPointsResult.Error?.ToString() }
                };

                if (_lastEndpointsResult == null)
                    return new ProgrammaticException("Response not arrived from Consul. " +
                                                     "This should not happen, ConsulDiscoverySource should await until a result is returned from Consul.", unencrypted: tags);

                else if (endPointsResult.Error != null)
                    return new EnvironmentException("Error calling Consul. See tags for details.", unencrypted: tags);
                else if (endPointsResult.IsQueryDefined == false)
                    return new EnvironmentException("Service doesn't exist on Consul. See tags for details.", unencrypted: tags);
                else
                    return new EnvironmentException("No endpoint were specified in Consul for the requested service.", unencrypted: tags);
            }
            else
            {
                return new MissingHostException("All endpoints defined by Consul for the requested service are unreachable. " +
                                                "Please make sure the endpoints on Consul are correct and are functioning properly. " +
                                                "See tags for the name of the requested service, and address of consul from which they were loaded. " +
                                                "exception for one of the causes a remote host was declared unreachable.",
                    lastException,
                    unencrypted: new Tags
                    {
                        {"consulAddress", ConsulClient.ConsulAddress.ToString()},
                        { "unreachableHosts", unreachableHosts },
                        {"requestedService", DeploymentName},
                        { "innerExceptionIsForEndPoint", lastExceptionEndPoint }
                    });
            }
        }

        public override void ShutDown()
        {
            ShutdownToken.Cancel();
        }

        public static string GetDeploymentName(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoverySettings)
        {
            if (serviceDiscoverySettings.Scope == ServiceScope.DataCenter)
            {
                return serviceDeployment.ServiceName;
            }
            return $"{serviceDeployment.ServiceName}-{serviceDeployment.DeploymentEnvironment}";
        }

    }
}
