using System;
using System.Collections.Generic;

using Gigya.Microdot.ServiceContract.HttpService;
using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{
	/// <summary>
	/// Contains the metadata for establishing the service endpoing and resolving calls handled by a service.
	/// </summary>
    public interface IServiceEndPointDefinition
    {
		/// <summary>
		/// True to use an encrypted communication channel, false to allow plaintext communication.
		/// </summary>
        bool UseSecureChannel { get; }

        int SiloGatewayPort { get; }

        int SiloNetworkingPort { get; }

        // Secondary nodes without ZooKeeper are only supported on a developer's machine (or unit tests), so
        // localhost and the original base port are always assumed (since the secondary nodes must use a
        // base port override to avoid port conflicts).
        int SiloNetworkingPortOfPrimaryNode { get; }

        int HttpPort { get; }

        /// <summary>
        /// Provides a friendly name for each service, keyed by the type of the service interface (the one decorated
        /// with <see cref="HttpServiceAttribute"/>).
        /// </summary>
        Dictionary<Type, string> ServiceNames { get; }

		/// <summary>
		/// Determines which service method should be called for the specified <see cref="InvocationTarget"/>.
		/// </summary>
		/// <param name="target">The invocation target specified by the remote client.</param>
		/// <returns></returns>
        ServiceMethod Resolve(InvocationTarget target);

        /// <summary>
        /// Returns a list of all grain methods 
        /// </summary>
        /// <returns></returns>
	    ServiceMethod[] GetAll();
    }
}