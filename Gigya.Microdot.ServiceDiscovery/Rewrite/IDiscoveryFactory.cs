using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Factory to get Discovery components: LoadBalancer and NodeSource
    /// </summary>
    public interface IDiscoveryFactory
    {
        /// <summary>
        /// Creates a new <see cref="ILoadBalancer"/> for the given <see cref="DeploymentIdentifier"/>. 
        /// A <see cref="ILoadBalancer"/> can be used to get a reachable node for the specific service at the specific environment
        /// </summary>
        /// <param name="deploymentIdentifier">identifier for service and env for which LoadBalancer is requested</param>
        /// <param name="reachabilityCheck">a function which checks whether a specified node is reachable, in order to monitor when unreachable nodes returns to be reachable</param>
        /// <returns>a valid <see cref="ILoadBalancer"/>, or null if the service is not implemented in the requested environment</returns>
        Task<ILoadBalancer> TryCreateLoadBalancer(DeploymentIdentifier deploymentIdentifier, ReachabilityCheck reachabilityCheck);

        /// <summary>
        /// Creates a new <see cref="INodeSource"/> for the given <see cref="DeploymentIdentifier"/>.
        /// A <see cref="INodeSource"/> can be used to get a list of nodes for the specific service at the specific environment
        /// </summary>
        /// <param name="deploymentIdentifier">identifier for service and env for which LoadBalancer is requested</param>
        /// <returns>a valid <see cref="INodeSource"/>, or null if the service is not implemented in the requested environment</returns>
        Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier deploymentIdentifier);
    }
}
