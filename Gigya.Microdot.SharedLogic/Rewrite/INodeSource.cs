using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.SharedLogic.Rewrite
{
    /// <summary>
    /// A source which supplies updated list of available nodes for discovery of a specific service and environment
    /// </summary>
    public interface INodeSource: IDisposable
    {
        /// <summary>
        /// Initialize this source (e.g. Consul source needs initialization to start monitoring Consul)
        /// </summary>
        /// <returns></returns>
        Task Init();

        /// <summary>
        /// Type of this source (used as discovery's "Source" entry on configuration)
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Returns current list of available nodes. Throws exception if no nodes are available.
        /// </summary>
        /// <returns></returns>
        INode[] GetNodes();

        /// <summary>
        /// Returns true only if service was undeployed on the nodes source (e.g. Consul)
        /// </summary>
        bool WasUndeployed { get; }

        /// <summary>
        /// Whether this source supports services which are deployed on multiple environemnts
        /// </summary>
        bool SupportsMultipleEnvironments { get; }
    }
}
