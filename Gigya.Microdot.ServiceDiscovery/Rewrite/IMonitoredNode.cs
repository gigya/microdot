using System;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// A node which can be monitored whether in is reachable or unreachable
    /// </summary>
    public interface IMonitoredNode
    {
        string Hostname { get; }
        int? Port { get; }

        /// <summary>
        /// Report that this node was responsive
        /// </summary>
        void ReportReachable();

        /// <summary>
        /// Report that this node was unresponsive and throw an exception which states that the node could not be reached
        /// </summary>
        /// <param name="ex"></param>
        void ReportUnreachable(Exception ex = null);
    }
}