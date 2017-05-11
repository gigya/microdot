using System;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.HostManagement
{
    public class OverriddenRemoteHost : RemoteHost
    {
        private string ServiceName { get; }

        internal OverriddenRemoteHost(string serviceName, string hostName, RemoteHostPool remoteHostPool) : base(hostName,remoteHostPool,new object())
        {
            ServiceName = serviceName;
        }

        public override bool ReportFailure(Exception ex = null)
        {
            throw new MissingHostException("Failed to reach an overridden remote host. Please make sure the " +
                "overrides specified are reachable from all services that participate in the request. See inner " +
                "exception for details and tags for information on which override caused this issue.",
                ex,
                unencrypted: new Tags
                {
                    { "overriddenServiceName", ServiceName },
                    { "overriddenEndpoint", HostName },
                    { "originalHostList", HostPool.GetAllHosts() }
                });
        }
    }
}