using System;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// This class represents a node which was manually overridden (by configuration).
    /// TODO: this class should be deleted after Discovery Rewrite is completed.
    /// </summary>
    [Obsolete("Delete this class after Discovery Rewrite is completed")]
    public class OverriddenNode : IMonitoredNode
    {
        private string ServiceName { get; }
        public string Hostname { get; }
        public int? Port { get; }


        internal OverriddenNode(string serviceName, string hostName, int? port = null)
        {
            ServiceName = serviceName;
            Hostname = hostName;
            Port = port;

        }



        public void ReportUnreachable(Exception ex = null)
        {
            throw new ServiceUnreachableException("Failed to reach an overridden node. Please make sure the " +
                                                  "overrides specified are reachable from all services that participate in the request. See inner " +
                                                  "exception for details and tags for information on which override caused this issue.",
                ex,
                unencrypted: new Tags
                {
                    { "overriddenServiceName", ServiceName },
                    { "overriddenHostName", Hostname },
                    { "overriddenPort", Port?.ToString() }

                });
        }

        public bool IsReachable => true;
        public Exception LastException => null;

        public void ReportReachable()
        {
        }

        public void Dispose()
        { }
    }
}