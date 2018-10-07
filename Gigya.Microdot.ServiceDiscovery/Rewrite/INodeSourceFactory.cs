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

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Creates new instances of INodeSource for the specified <see cref="Type"/> of source
    /// </summary>
    public interface INodeSourceFactory : IDisposable
    {
        /// <summary>
        /// Type of node source which this factory can create (used in the "Source" entry of the discovery configuration)
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Reports whether a service is known to be deployed.
        /// </summary>
        Task<bool> IsServiceDeployed(DeploymentIdentifier deploymentIdentifier);

        /// <summary>
        /// Creates a new <see cref="INodeSource"/> for the given <see cref="DeploymentIdentifier"/>.
        /// A <see cref="INodeSource"/> can be used to get a list of nodes for the specific service at the specific environment.
        /// Call <see cref="IsServiceDeployed"/> before creating a node source, and continuously afterwards to
        /// detect when the node source is no longer valid and should be disposed.
        /// </summary>
        Task<INodeSource> CreateNodeSource(DeploymentIdentifier deploymentIdentifier);
    }
}