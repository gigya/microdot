using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public interface IAvailabilityZoneServiceDiscovery
    {
        Task<bool> HandleEnvironmentChangesAsync();
        AvailabilityZoneInfo Info { get; }
        TimeSpan DiscoveryGetNodeTimeoutInMs { get; set; }
    }
}
