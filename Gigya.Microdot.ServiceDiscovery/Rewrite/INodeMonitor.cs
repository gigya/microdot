using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors current nodes state
    /// </summary>
    public interface INodeMonitor: IDisposable
    {
        /// <summary>
        /// Initialize monitoring of Consul nodes
        /// </summary>
        /// <returns></returns>
        Task Init();

        /// <summary>
        /// List of nodes for this service at current active version
        /// </summary>
        INode[] Nodes { get; }

        /// <summary>
        /// Whether this service has been undeployed
        /// </summary>
        bool WasUndeployed { get; }

    }
}