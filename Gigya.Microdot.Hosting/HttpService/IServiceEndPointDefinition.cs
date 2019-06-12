#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic.HttpService;

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

        /// <summary>
        /// Secondary nodes without ZooKeeper are only supported on a developer's machine (or unit tests), so
        /// localhost and the original base port are always assumed (since the secondary nodes must use a
        /// base port override to avoid port conflicts).
        ///</summary> 
        int? SiloNetworkingPortOfPrimaryNode { get; }

        int HttpPort { get; }

        /// <summary>
        /// Provides a friendly name for each service, keyed by the type of the service interface (the one decorated
        /// with <see cref="HttpServiceAttribute"/>).
        /// </summary>
        Dictionary<Type, string> ServiceNames { get; }

        int SiloDashboardPort { get; }

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

        EndPointMetadata GetMetaData(ServiceMethod method);

    }
}