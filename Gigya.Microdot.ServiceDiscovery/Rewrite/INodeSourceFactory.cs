using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Creates new instances of INodeSource for the specified <see cref="Type"/> of source
    /// </summary>
    public interface INodeSourceFactory : IDisposable
    {
        /// <summary>
        /// Type of node source which this factory can create (used in the "Source" entry of the discovery configuration)
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Reports whether a service is known to be deployed.
        /// </summary>
        bool IsServiceDeployed(DeploymentIdentifier deploymentIdentifier);

        /// <summary>
        /// Creates a new <see cref="INodeSource"/> for the given <see cref="DeploymentIdentifier"/>.
        /// A <see cref="INodeSource"/> can be used to get a list of nodes for the specific service at the specific environment.
        /// Call <see cref="IsServiceDeployed"/> before creating a node source, and continuously afterwards to
        /// detect when the node source is no longer valid and should be disposed.
        /// </summary>
        Task<INodeSource> CreateNodeSource(DeploymentIdentifier deploymentIdentifier);
    }
}