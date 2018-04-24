using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public interface INewServiceDiscovery
    {
        /// <summary>
        /// Retrieves the next reachable <see cref="IMonitoredNode"/>.
        /// </summary>
        /// <returns>A reachable <see cref="IMonitoredNode"/>.</returns>
        /// <exception cref="EnvironmentException">Thrown when there is no reachable <see cref="IMonitoredNode"/> available.</exception>
        Task<IMonitoredNode> GetNode();
    }
}