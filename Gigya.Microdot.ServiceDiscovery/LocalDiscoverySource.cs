using System;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.ServiceDiscovery
{
    /// <summary>
    /// Returns the current computer as the sole node in the list of endpoints. Never changes.
    /// </summary>
    public class LocalDiscoverySource : ServiceDiscoverySourceBase
    {
        public LocalDiscoverySource(string serviceName) : base($"{CurrentApplicationInfo.HostName}-{serviceName}")
        {
            EndPoints = new[] { new EndPoint { HostName = CurrentApplicationInfo.HostName } };
        }

        public override Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts)
        {
            return new MissingHostException("Service source is configured to 'Local' Discovery mode, but is not reachable on local machine. See tags for more details.",
                lastException,
                unencrypted: new Tags
                {
                    {"unreachableHosts", unreachableHosts},
                    {"configPath", $"Discovery.{DeploymentName}.Source"},
                    {"requestedService", DeploymentName},
                    {"innerExceptionIsForEndPoint", lastExceptionEndPoint}
                });
        }


        public override bool IsServiceDeploymentDefined { get; } = true;
    }

}