using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public interface INewServiceDiscovery
    {
        /// <summary>
        /// Retrieves the next reachable <see cref="MonitoredNode"/>.
        /// </summary>
        /// <returns>A reachable <see cref="MonitoredNode"/>.</returns>
        /// <exception cref="EnvironmentException">Thrown when there is no reachable <see cref="MonitoredNode"/> available.</exception>
        Task<MonitoredNode> GetNode();
    }
}