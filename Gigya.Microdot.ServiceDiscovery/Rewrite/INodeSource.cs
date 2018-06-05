using System;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// A source which supplies updated list of available nodes for discovery of a specific service and environment
    /// </summary>
    public interface INodeSource: IDisposable
    {
        /// <summary>
        /// Returns all nodes. Throws detailed exception if no nodes are available which includes the source's reason.
        /// </summary>
        /// <returns>A non-empty array of nodes.</returns>
        /// <exception cref="EnvironmentException">Thrown when no nodes are available, the service was undeployed or an error occurred.</exception>
        Node[] GetNodes();
    }
}
