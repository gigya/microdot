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
using System.Linq;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceDiscovery
{
    /// <summary>
    /// Returns a list of endpoints from configuration. Note: When the configuration changes, this object is recreated
    /// with updated settings, hence we don't listen to changes ourselves.
    /// </summary>
    public class ConfigDiscoverySource : ServiceDiscoverySourceBase
    {
        public override string SourceName => "Config";

        private readonly ServiceDiscoveryConfig _serviceDiscoveryConfig;

        private ILog Log { get; }

        private string ConfigPath => $"Discovery.{Deployment}";

        public ConfigDiscoverySource(DeploymentIdentifier deployment, Func<DiscoveryConfig> getConfig, ILog log) : base(deployment.ServiceName)
        {            
            _serviceDiscoveryConfig = getConfig().Services[deployment.ServiceName];
            Log = log;
            Result = new EndPointsResult {EndPoints = GetEndPointsInitialValue()};
        }

        private EndPoint[] GetEndPointsInitialValue()
        {
            var hosts = _serviceDiscoveryConfig.Hosts ?? string.Empty;
            var endPoints = hosts.Split(',', '\r', '\n')
                                 .Where(e => !string.IsNullOrWhiteSpace(e))
                                 .Select(CreateEndpoint)
                                 .ToArray();

            Log.Debug(_ => _("Loaded RemoteHosts instance. See tags for details.", unencryptedTags: new
            {
                configPath = ConfigPath,
                componentName = Deployment,
                endPoints = endPoints
            }));

            return endPoints;           
        }

        private EndPoint CreateEndpoint(string host)
        {
            var parts = host
                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => string.IsNullOrEmpty(p) == false)
                .ToArray();

            if (parts.Length > 2)
                throw new ArgumentException("Host name must contain at most one colon (:).", nameof(host));

            var endpoint = new EndPoint
            {
                Port = parts.Length == 2 ? int.Parse(parts[1]) : _serviceDiscoveryConfig.DefaultPort,
                HostName = parts[0]
            };

            return endpoint;
        }


        public override bool IsServiceDeploymentDefined => true;
      


        public override Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts)
        {
            if (endPointsResult.EndPoints.Length == 0)
            {
                return new EnvironmentException("No endpoint were specified in the configuration for the " +
                                                "requested service. Please make sure you've specified a list of hosts for the requested " +
                                                "service in the configuration. If you're a developer and want to access a service on your " +
                                                "local machine, change service configuration to Discovery.[ServiceName].Mode=\"Local\". " +
                                                "See tags for the name of the service requested, and for the " +
                                                "configuration path where the list of endpoints are expected to be specified.",
                    unencrypted: new Tags
                    {
                        {"requestedService", Deployment},
                        {"missingConfigPath", $"Discovery.{Deployment}.Hosts"}
                    });
            }
            else
            {
                return new ServiceUnreachableException("All endpoints defined by the configuration for the requested service are unreachable. " +
                                                "Please make sure the remote hosts specified in the configuration are correct and are " +
                                                "functioning properly. See tags for the name of the requested service, the list of hosts " +
                                                "that are unreachable, and the configuration path they were loaded from. See the inner " +
                                                "exception for one of the causes a remote host was declared unreachable.",
                    lastException,
                    unencrypted: new Tags
                    {
                        { "unreachableHosts", unreachableHosts },
                        {"configPath", $"Discovery.{Deployment}.Hosts"},
                        {"requestedService", Deployment},
                        { "innerExceptionIsForEndPoint", lastExceptionEndPoint }
                    });
            }
        }


    }
}