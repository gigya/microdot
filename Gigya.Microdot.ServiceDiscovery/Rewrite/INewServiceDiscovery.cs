using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public interface INewServiceDiscovery
    {
        /// <summary>
        /// Retrieves a reachable <see cref="Node"/>, or null if service is not deployed.
        /// </summary>
        Task<Node> GetNode();
    }
}