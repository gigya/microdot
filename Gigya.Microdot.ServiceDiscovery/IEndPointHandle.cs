using System;

namespace Gigya.Microdot.ServiceDiscovery
{
    public interface IEndPointHandle
    {
        string HostName { get; }        
        
        // Used only with configuration BasePortOverride. Port is not loaded from Consul yet (should be in the future)
        int? Port { get; }

        /// <returns>True if this <see cref="IEndPointHandle"/> is still considered reachable despite the failure, or false if it has been marked as unreachable.</returns>
        bool ReportFailure(Exception ex = null);

        void ReportSuccess();
    }
}
