using System;
using System.Collections.Generic;
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
        ImmutableHashSet<string> Services { get; }
    }
}