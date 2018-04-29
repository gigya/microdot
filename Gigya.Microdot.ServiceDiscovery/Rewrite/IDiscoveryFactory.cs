using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public interface IDiscoveryFactory
    {
        /// <summary>
        /// Creates a <see cref="ILoadBalancer"/> for a given <see cref="DeploymentIdentifier"/>. If no such service is deployed, returns <see langword="null" />.
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="check"></param>
        /// <returns></returns>
        ILoadBalancer TryCreateLoadBalancer(DeploymentIdentifier identifier, ReachabilityCheck check);

        /// <summary>
        /// Creates a <see cref="INodeSource"/> for a given <see cref="DeploymentIdentifier"/>. If no such service is deployed, returns <see langword="null" />.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        INodeSource TryCreateNodeSource(DeploymentIdentifier identifier);
    }
}
