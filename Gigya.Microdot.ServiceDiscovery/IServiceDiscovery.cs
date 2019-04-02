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
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.ServiceDiscovery
{
    public delegate Task<bool> ReachabilityChecker(IEndPointHandle remoteHost);

    [Obsolete("Use IDiscovery instead")]
    public interface IServiceDiscovery
    {
        /// <summary>
        /// Retrieves the next reachable <see cref="IEndPointHandle"/>.
        /// </summary>
        /// <param name="affinityToken">
        /// A string to generate a consistent affinity to a specific host within the set of available hosts.
        /// Identical strings will return the same host for a given pool of reachable hosts. A request ID is usually provided.
        /// </param>
        /// <returns>A reachable <see cref="IEndPointHandle"/>.</returns>
        /// <exception cref="EnvironmentException">Thrown when there is no reachable <see cref="IEndPointHandle"/> available.</exception>
        Task<IEndPointHandle> GetNextHost(string affinityToken = null);

        Task<IEndPointHandle> GetOrWaitForNextHost(CancellationToken cancellationToken);

        /// <summary>
        /// Provides notification when the list of EndPoints for this service has changed. The name of the deployment
        /// environment is provided, which Gator should be used to refresh the schema.
        /// </summary>
        ISourceBlock<string> EndPointsChanged { get; }

        /// <summary>
        /// Provides notification when a service becomes reachable or unreachable. The current reachability status
        /// of the service is provided, which Gator should be used to refresh the schema.
        /// </summary>
        ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged { get; }

        //Returns all Endpoints that service discovery is aware of, it takes fallback enviroment logic into account.
        Task<EndPoint[]> GetAllEndPoints();
    }
}