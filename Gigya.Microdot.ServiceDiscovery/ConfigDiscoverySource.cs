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
        private readonly ServiceDiscoveryConfig _serviceDiscoveryConfig;

        private ILog Log { get; }

        private string ConfigPath => $"Discovery.{DeploymentName}";

        public ConfigDiscoverySource(string deploymentName, ServiceDiscoveryConfig serviceDiscoveryConfig, ILog log) : base(deploymentName)
        {
            _serviceDiscoveryConfig = serviceDiscoveryConfig;
            Log = log;
            EndPoints = GetEndPointsInitialValue();
        }

        private EndPoint[] GetEndPointsInitialValue()
        {
            var hosts = _serviceDiscoveryConfig.Hosts ?? string.Empty;
            var endPoints = hosts.Split(',', '\r', '\n')
                                 .Where(e => !string.IsNullOrWhiteSpace(e))
                                 .Select(h => new EndPoint { HostName = h.Trim(), Port = _serviceDiscoveryConfig.DefaultPort })
                                 .ToArray();

            Log.Debug(_ => _("Loaded RemoteHosts instance. See tags for details.", unencryptedTags: new
            {
                configPath = ConfigPath,
                componentName = DeploymentName,
                endPoints = EndPoints
            }));

            return endPoints;           
        }


        public override bool IsServiceDeploymentDefined { get; } = true;


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
                        {"requestedService", DeploymentName},
                        {"missingConfigPath", $"Discovery.{DeploymentName}.Hosts"}
                    });
            }
            else
            {
                return new MissingHostException("All defined hosts for the requested service are unreachable. " +
                                                "Please make sure the remote hosts specified in the configuration are correct and are " +
                                                "functioning properly. See tags for the name of the requested service, the list of hosts " +
                                                "that are unreachable, and the configuration path they were loaded from. See the inner " +
                                                "exception for one of the causes a remote host was declared unreachable.",
                    lastException,
                    unencrypted: new Tags
                    {
                        { "unreachableHosts", unreachableHosts },
                        {"configPath", $"Discovery.{DeploymentName}.Hosts"},
                        {"requestedService", DeploymentName},
                        { "innerExceptionIsForEndPoint", lastExceptionEndPoint }
                    });
            }
        }


    }
}