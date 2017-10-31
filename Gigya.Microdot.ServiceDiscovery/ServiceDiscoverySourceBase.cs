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
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.ServiceDiscovery
{
    /// <summary>
    /// Provides an up-to-date list of endpoints.
    /// </summary>
    public abstract class ServiceDiscoverySourceBase : IServiceDiscoverySource
    {        

        public string Deployment { get; }
        public EndPointsResult Result { get; protected set; } = new EndPointsResult{EndPoints = new EndPoint[0]};

        protected BroadcastBlock<EndPointsResult> EndpointsChangedBroadcast = new BroadcastBlock<EndPointsResult>(null);
        public ISourceBlock<EndPointsResult> EndPointsChanged => EndpointsChangedBroadcast;

        public abstract bool IsServiceDeploymentDefined { get; }
        public abstract string SourceName { get; }
        public virtual bool SupportsFallback => false;


        public virtual Task Init() => Task.FromResult(1);

        protected ServiceDiscoverySourceBase(string deployment)
        {
            Deployment = deployment;
        }



        public abstract Exception AllEndpointsUnreachable(
            EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts);


        public virtual void ShutDown()
        {
            EndPointsChanged?.Complete();
        }


        protected virtual void Dispose(bool disposing)
        {
            if(disposing) {                
                ShutDown();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
