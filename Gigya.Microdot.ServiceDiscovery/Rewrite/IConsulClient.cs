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
using System.Threading;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Client for accessing Consul
    /// </summary>
    public interface IConsulClient: IDisposable
    {
        /// <summary>
        /// Get all keys from the Consul's key-value store
        /// </summary>
        /// <param name="modifyIndex">The modifyIndex of last response from Consul, to be used for long-polling. Should be zero (0) When calling Consul for the first time</param>
        /// <param name="folder">folder of key-value store (e.g. "service", "flags")</param>
        /// <param name="cancellationToken">Token for cancelling the call to Consul</param>
        /// <returns></returns>
        Task<ConsulResponse<string[]>> GetAllKeys(ulong modifyIndex, string folder, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>
		/// Get the value of a key on Consul's key-value store, for Consul which is located on other Zone
		/// </summary>
		/// <exception cref="ArgumentNullException">
        /// Throws if given folder or key are null, or if consul Zone is not set
        /// </exception>
        /// <exception cref="Exception">
        /// Throws if failed to deserialize consul json value
        /// </exception>
        /// <returns>Deserialized T of read key value from consul</returns>
		/// <typeparam name="T">Type to be deserialized (Json) when reading the key value from Consul</typeparam>
		/// <param name="modifyIndex">The modifyIndex of last response from Consul, to be used for long-polling. Should be zero (0) When calling Consul for the first time</param>
		/// <param name="folder">folder of key-value store (e.g. "service", "flags")</param>
		/// <param name="key">the key which its value is requested</param>
		/// <param name="cancellationToken">Token for cancelling the call to Consul</param>        
		Task<ConsulResponse<T>> GetKey<T>(ulong modifyIndex, string folder, string key, CancellationToken cancellationToken = default(CancellationToken)) where T : class;

		/// <summary>
		/// Get the value of a key on Consul's key-value store
		/// </summary>
		/// <typeparam name="T">Type to be deserialized (Json) when reading the key value from Consul</typeparam>
		/// <param name="modifyIndex">The modifyIndex of last response from Consul, to be used for long-polling. Should be zero (0) When calling Consul for the first time</param>
		/// <param name="folder">folder of key-value store (e.g. "service", "flags")</param>
		/// <param name="key">the key which its value is requested</param>
		/// <param name="zone">zone where the key-value is requested</param>
		/// <param name="cancellationToken">Token for cancelling the call to Consul</param>        
		Task<ConsulResponse<T>> GetKeyFromOtherZone<T>(ulong modifyIndex, string folder, string key, string zone, CancellationToken cancellationToken = default(CancellationToken)) where T : class;


        Task<ConsulResponse<ConsulNode[]>> GetHealthyNodes(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken);
    }
}
