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
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic.HttpService;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy
{
    public interface IServiceProxyProvider<TInterface>
    {
        TInterface Client { get; }
    }


    public interface IServiceProxyProvider
    {
        Task<object> Invoke(HttpServiceRequest request, Type resultReturnType);
        Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings);
        Task<ServiceSchema> GetSchema();
        ISourceBlock<string> EndPointsChanged { get; }
        ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged { get; }
        int? DefaultPort { get; set; }
        bool ServiceInterfaceRequiresHttps { get; set; }
        string ServiceName { get;  }
        Action<HttpServiceRequest> PrepareRequest { get; set; }
        void SetHttpTimeout(TimeSpan timeout);
    }
}