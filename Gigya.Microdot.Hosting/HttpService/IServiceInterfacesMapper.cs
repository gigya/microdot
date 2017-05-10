using System;
using System.Collections.Generic;

namespace Gigya.Microdot.Hosting.HttpService
{
	/// <summary>
	/// Mapping between service interfaces to grain interfaces. Used in Orleans for additional type information (for creating grain references).
	/// The map is an identity map outside orleans.
	/// </summary>
    public interface IServiceInterfaceMapper
    {
		/// <summary>
		/// The service interface discovered in this application.
		/// </summary>
		IEnumerable<Type> ServiceInterfaceTypes { get; }

        /// <summary>
        /// The service implementing IHealthStatus
        /// </summary>
	    Type HealthStatusServiceType { get; set; }


	    /// <summary>
		/// A method that maps a service interface to its corresponding grain interface. Used only in Orleans.
		/// </summary>
		/// <param name="serviceInterface">The service interface to map.</param>
		/// <returns>The grain interface for the provided service interface, or the provideded parameter (identity) when not used in Orelans.</returns>
		Type GetGrainInterface(Type serviceInterface);
    }

}
