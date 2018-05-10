using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors Consul using KeyValue api, to get a list of all available services
    /// </summary>    
    public interface IConsulServiceListMonitor: IDisposable
    {
        Task Init();

        /// <summary>
        /// List of all known services in this data center. WARNING: Service names casing might differ than service discovery configurations.
        /// This list is case insensitive.
        /// </summary>
        ImmutableHashSet<string> Services { get; }

        /// <summary>
        /// Incremented whenever the <see cref="Services"/> list is modified.
        /// </summary>
        int Version { get; }
    }
}