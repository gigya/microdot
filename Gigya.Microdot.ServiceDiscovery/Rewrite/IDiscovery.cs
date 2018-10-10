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
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Factory to get Discovery components: LoadBalancer and NodeSource
    /// </summary>
    public interface IDiscovery: IDisposable
    {
        /// <summary>
        /// Creates a new <see cref="ILoadBalancer"/> for the given <see cref="DeploymentIdentifier"/>. 
        /// A <see cref="ILoadBalancer"/> can be used to get a reachable node for the specific service at the specific environment
        /// </summary>
        /// <param name="deploymentIdentifier">identifier for service and env for which LoadBalancer is requested</param>
        /// <param name="reachabilityCheck">a function which checks whether a specified node is reachable, in order to monitor when unreachable nodes returns to be reachable</param>
        /// <param name="trafficRoutingStrategy">The strategy of traffic routing to be used in order to decide which node should be returned for each request</param>
        /// <returns>a valid <see cref="ILoadBalancer"/>, or null if the service is not implemented in the requested environment</returns>
        ILoadBalancer CreateLoadBalancer(DeploymentIdentifier deploymentIdentifier, ReachabilityCheck reachabilityCheck, TrafficRoutingStrategy trafficRoutingStrategy);

        /// <summary>
        /// Returns a list of nodes for the given <see cref="DeploymentIdentifier"/>.
        /// Returns null If the service is not deployed on this environment
        /// </summary>
        /// <param name="deploymentIdentifier">identifier for service and env for which LoadBalancer is requested</param>
        /// <returns>a list of <see cref="Node"/>, or null if the service is not deployed in the requested environment</returns>
        Task<Node[]> GetNodes(DeploymentIdentifier deploymentIdentifier);
    }
}
