using System;

namespace Gigya.Microdot.SharedLogic.Rewrite
{
    /// <summary>
    /// A source which supplies updated list of available nodes for discovery of a specific service and environment
    /// </summary>
    public interface INodeSource: IDisposable
    {
        /// <summary>
        /// Name of this source (used as discovery's "Source" entry on configuration)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns current list of available nodes
        /// </summary>
        /// <returns></returns>
        INode[] GetNodes();

        /// <summary>
        /// Whether this source is active for current service and environment
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Whether this source supports services which are deployed on multiple environemnts
        /// </summary>
        bool SupportsMultipleEnvironments { get; }
    }
}
