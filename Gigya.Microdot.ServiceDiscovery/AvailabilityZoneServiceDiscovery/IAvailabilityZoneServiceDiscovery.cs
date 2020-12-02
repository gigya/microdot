using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    /// <summary>
    /// Runs consul polling thread and updates DeploymentIdentifier according to ZoneSettings
    /// </summary>
    /// <remarks>
    /// <para>Upon instantiation a thread will start polling consul for ZoneSettings for the given service</para>
    /// </remarks>
    /// <exception cref="EnvironmentException">
    /// Upon one of the failures described at AvailabilityZoneInfo.StatusCodes, a detailed message with tags will describe the issue and caller can handle health checks accordingly
    /// </exception>
    public interface IAvailabilityZoneServiceDiscovery
    {
        /// <summary>
        /// Updates Info with new nodes if discovered
        /// </summary>
        /// <returns>
        /// true - if changes in nodes list (and therefor in DeploymentIdentifier had occured)
        /// </returns>
        Task<bool> HandleEnvironmentChangesAsync();
        AvailabilityZoneInfo Info { get; }
        /// <summary>
        /// Task Cancellation Source which will be set upon first SUCCESSFUL connection with consul.
        /// Upon initiating AvailabilityZoneServiceDiscovery instance - this task should be awaited before use
        /// </summary>
        /// <returns>Task to be awaited</returns>
        Task GetInitialReadZonesTcs();

    }
}