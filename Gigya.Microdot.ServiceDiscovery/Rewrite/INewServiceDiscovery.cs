using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public interface INewServiceDiscovery
    {
        /// <summary>
        /// Retrieves a LoadBalancer which can be used to get a reachable <see cref="IMonitoredNode"/>.
        /// </summary>
        Task<ILoadBalancer> GetLoadBalancer();
    }
}