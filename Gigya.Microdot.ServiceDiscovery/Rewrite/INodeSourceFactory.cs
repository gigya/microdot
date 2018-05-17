using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Creates new instances of INodeSource for the specified <see cref="Type"/> of source
    /// </summary>
    public interface INodeSourceFactory
    {
        /// <summary>
        /// Type of node source which this factory can create (used in the "Source" entry of the discovery configuration)
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Creates a new <see cref="INodeSource"/> for the given <see cref="DeploymentIdentifier"/>.
        /// A <see cref="INodeSource"/> can be used to get a list of nodes for the specific service at the specific environment
        /// </summary>
        /// <param name="deploymentIdentifier">identifier for service and env for which LoadBalancer is requested</param>
        /// <returns>a valid <see cref="INodeSource"/>, or null if the service is not implemented in the requested environment</returns>
        Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier deploymentIdentifier);
    }
}