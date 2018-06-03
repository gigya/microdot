using System;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.SharedLogic.Rewrite
{
    /// <summary>
    /// A source which supplies updated list of available nodes for discovery of a specific service and environment
    /// </summary>
    public interface INodeSource 
    {
        /// <summary>
        /// Returns all nodes. Throws detailed exception if no nodes are available which includes the source's reason.
        /// </summary>
        /// <returns>A non-empty array of nodes.</returns>
        /// <exception cref="EnvironmentException">Thrown when no nodes are available, the service was undeployed or an error occurred.</exception>
        Node[] GetNodes();

        /// <summary>
        /// Returns true if the service was undeployed.
        /// </summary>
        bool WasUndeployed { get; }

        void Dispose();

        /// <summary>
        /// Type of this Source. This type should be matching to the config entry 'Source' (at: Discovery.Services.[serviceName].Source)
        /// </summary>
        string Type { get; }
    }
}
