using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public interface IAvailabilityZoneServiceDiscovery
    {
        Task<bool> HandleEnvironmentChangesAsync();
        AvailabilityZoneInfo Info { get; }
        TimeSpan DiscoveryGetNodeTimeoutInMs { get; set; }
    }
}