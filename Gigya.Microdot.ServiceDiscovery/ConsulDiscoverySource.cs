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
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulDiscoverySource : ServiceDiscoverySourceBase
    {
        public const string Name = "Consul";

        public override string SourceName => Name;
        public override bool SupportsFallback => true;
        public override bool IsServiceDeploymentDefined => ConsulClient.Result.IsQueryDefined;

        private IConsulClient ConsulClient { get; set; }

        private readonly Func<string, IConsulClient> _getConsulClient;
        private readonly ILog _log;

        private readonly object _resultLocker = new object();

        private readonly TaskCompletionSource<bool> _initialized;

        private bool _firstTime = true;

        private EndPointsResult _lastResult;
        private IDisposable _resultChangedLink;
        private readonly object _initLock = new object();

        private bool _disposed; 

        public ConsulDiscoverySource(ServiceDeployment serviceDeployment,
            Func<DiscoveryConfig> getConfig,
            Func<string, IConsulClient> getConsulClient, ILog log)
            : base(GetDeploymentName(serviceDeployment, getConfig().Services[serviceDeployment.ServiceName]))

        {            
            _getConsulClient = getConsulClient;            
            _log = log;

            _initialized = new TaskCompletionSource<bool>();            
        }

        public override Task Init()
        {
            ConsulClient = _getConsulClient(Deployment);
            lock (_initLock)
                if (_resultChangedLink==null)
                    _resultChangedLink = ConsulClient.ResultChanged.LinkTo(new ActionBlock<EndPointsResult>(r => ConsulResultChanged(r)));

            ConsulClient.Init();
            return _initialized.Task;
        }

        private void ConsulResultChanged(EndPointsResult newResult)
        {
            lock (_resultLocker)
            {
                var shouldReportChanges = false;
                if (newResult.IsQueryDefined != Result.IsQueryDefined && !_firstTime)
                {
                    shouldReportChanges = true;
                    if (newResult.IsQueryDefined==false)
                        _log.Warn(x => x("Service has become undefined on Consul", unencryptedTags: new {serviceName = Deployment}));
                    else
                        _log.Info(x => x("Service has become defined on Consul", unencryptedTags: new { serviceName = Deployment }));
                }
                else if (!OrderedEndpoints(newResult.EndPoints).SequenceEqual(OrderedEndpoints(Result.EndPoints)))
                {
                    shouldReportChanges = true;
                    _log.Info(_ => _("Obtained a new list of endpoints for service from Consul", unencryptedTags: new
                    {
                        serviceName = Deployment,
                        endpoints = string.Join(", ", newResult.EndPoints.Select(e => e.HostName + ':' + (e.Port?.ToString() ?? "")))
                    }));
                }

                if (_firstTime || newResult.Error == null || Result.Error != null)
                    Result = newResult;

                if (shouldReportChanges && !_firstTime)
                    EndpointsChangedBroadcast.Post(Result);

                _lastResult = newResult;
                _firstTime = false;
                _initialized.TrySetResult(true);
            }
        }

        private IEnumerable<EndPoint> OrderedEndpoints(IEnumerable<EndPoint> endpoints)
        {
            return endpoints.OrderBy(x => x.HostName).ThenBy(x => x.Port);
        }

        public override Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts)
        {
            if (endPointsResult.EndPoints.Length == 0)
            {
                var tags = new Tags
                {
                    {"requestedService", Deployment},
                    {"consulAddress", ConsulClient?.ConsulAddress?.ToString()},
                    { "requestTime", endPointsResult.RequestDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                    { "requestLog", endPointsResult.RequestLog },
                    { "responseLog", endPointsResult.ResponseLog },
                    { "queryDefined", endPointsResult.IsQueryDefined.ToString() },
                    { "consulError", endPointsResult.Error?.ToString() },
                    { "activeVersion", endPointsResult.ActiveVersion }
                };

                if (_lastResult == null)
                    return new ProgrammaticException("Response not arrived from Consul. " +
                                                     "This should not happen, ConsulDiscoverySource should await until a result is returned from Consul.", unencrypted: tags);

                else if (endPointsResult.Error != null)
                    return new EnvironmentException("Error calling Consul. See tags for details.", unencrypted: tags);
                else if (endPointsResult.IsQueryDefined == false)
                    return new EnvironmentException("Service doesn't exist on Consul. See tags for details.", unencrypted: tags);
                else
                    return new EnvironmentException("No endpoint were specified in Consul for the requested service and service's active version.", unencrypted: tags);
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
                        {"requestedService", Deployment},
                        { "innerExceptionIsForEndPoint", lastExceptionEndPoint }
                    });
            }
        }

        public override void ShutDown()
        {
            if (!_disposed)
            {
                _resultChangedLink?.Dispose();
                ConsulClient?.Dispose();
            }

            _disposed = true;
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
