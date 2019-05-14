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
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{

    public class NodeAndLoadBalancer
    {
        public Node Node { get; set; }
        public ILoadBalancer LoadBalancer { get; set; }

        public string PreferredEnvironment { get; set; }
        public string TargetEnvironment { get; set; }
    }


    public interface IMultiEnvironmentServiceDiscovery
    {
        /// <summary>
        /// Retrieves a reachable <see cref="Node"/>, or null if service is not deployed. Also optionally returns a <see cref="ILoadBalancer"/>. You should call
        /// <see cref="ILoadBalancer.ReportUnreachable(Node, Exception)"/> in case you couldn't communicate with the <see cref="Node"/>
        /// </summary>
        /// <exception cref="ServiceUnreachableException">If the service is not deployed (in either the current, preferred, or prod environemnt), or
        /// if all host of the service in the relevant environment are not reachable.</exception>
        Task<NodeAndLoadBalancer> GetNode();
    }
}