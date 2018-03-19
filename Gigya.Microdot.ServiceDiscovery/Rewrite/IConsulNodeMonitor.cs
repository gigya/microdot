using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors Consul using Health api and KeyValue api, to find the current active version of a service, and get its nodes list
    /// </summary>
    public interface IConsulNodeMonitor: IDisposable
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
    }
}