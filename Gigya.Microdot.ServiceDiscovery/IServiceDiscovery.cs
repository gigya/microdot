using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.ServiceContract.Exceptions;
using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.ServiceDiscovery
{
    public delegate Task<bool> ReachabilityChecker(IEndPointHandle remoteHost);

    public interface IServiceDiscovery
    {
        /// <summary>
        /// Retrieves the next reachable <see cref="IEndPointHandle"/>.
        /// </summary>
        /// <param name="affinityToken">
        /// A string to generate a consistent affinity to a specific host within the set of available hosts.
        /// Identical strings will return the same host for a given pool of reachable hosts. A request ID is usually provided.
        /// </param>
        /// <returns>A reachable <see cref="IEndPointHandle"/>.</returns>
        /// <exception cref="EnvironmentException">Thrown when there is no reachable <see cref="IEndPointHandle"/> available.</exception>
        Task<IEndPointHandle> GetNextHost(string affinityToken = null);

        Task<IEndPointHandle> GetOrWaitForNextHost(CancellationToken cancellationToken);

        /// <summary>
        /// Provides notification when the list of EndPoints for this service has changed. The name of the deployment
        /// environment is provided, which Gator should be used to refresh the schema.
        /// </summary>
        ISourceBlock<string> EndPointsChanged { get; }

        /// <summary>
        /// Provides notification when a service becomes reachable or unreachable. The current reachability status
        /// of the service is provided, which Gator should be used to refresh the schema.
        /// </summary>
        ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged { get; }
    }
}