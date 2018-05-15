using System;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// A node which can be monitored whether in is reachable or unreachable
    /// </summary>
    public interface IMonitoredNode : INode
    {
        /// <summary>
        /// Report that this node was responsive
        /// </summary>
        void ReportReachable();

        /// <summary>
        /// Report that this node was unresponsive and throw an exception which states that the node could not be reached
        /// </summary>
        /// <param name="ex"></param>
        void ReportUnreachable(Exception ex = null);

        /// <summary>
        /// Whether this node is currently reachable or not
        /// </summary>
        bool IsReachable { get; }

        /// <summary>
        /// Last exception thrown, in case node is unreachable
        /// </summary>
        Exception LastException { get; }

        /// <summary>
        /// Stop the background monitoring of this node
        /// </summary>
        void StopMonitoring();
    }
}