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
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Utils;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
    public interface IServiceProxyProvider : IProxyable
    {
        Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings = null);
        Task<ServiceSchema> GetSchema();
        HttpServiceAttribute HttpSettings { get; }
    }

    public class ServiceProxyProvider : IServiceProxyProvider
    {
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.None
        };

        public HttpServiceAttribute HttpSettings { get; }
        
        /// <summary>
        /// Gets the name of the remote service from the interface name.
        /// is used.
        /// </summary>
        public string ServiceName { get; }

        private ConcurrentDictionary<string, DeployedService> Deployments { get; set; }

        public ServiceProxyProvider(string serviceName)
        {
            ServiceName = serviceName;
        }

        public object Invoke(MethodInfo targetMethod, object[] args)
        {
            // TODO: Add caching to this step to prevent using reflection every call
            var resultReturnType = targetMethod.ReturnType.GetGenericArguments().SingleOrDefault() ?? typeof(object);
            var request = new HttpServiceRequest(targetMethod, args);

            return TaskConverter.ToStronglyTypedTask(Invoke(request, resultReturnType), resultReturnType);
        }

        public async Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (resultReturnType == null)
                throw new ArgumentNullException(nameof(resultReturnType));


            request.Overrides = TracingContext.TryGetOverrides();
            request.TracingData = new TracingData
            {
                HostName = CurrentApplicationInfo.HostName?.ToUpperInvariant(),
                ServiceName = CurrentApplicationInfo.Name,
                RequestID = TracingContext.TryGetRequestID(),
                SpanID = Guid.NewGuid().ToString("N"), //Each call is new span                
                ParentSpanID = TracingContext.TryGetSpanID()
            };

            var hostOverride = TracingContext.GetHostOverride(ServiceName);


            if (hostOverride != null)
            {
                
            }
            else
            {
                
            }

            return null;
        }

        private HttpRequestMessage CreateHttpRequest(HttpServiceRequest serviceRequest, JsonSerializerSettings jsonSettings, string hostName, int port)
        {
    
            
            string uri = $"{(HttpSettings.UseHttps ? "https" : "http")}://{hostName}:{port}/{ServiceName}.{serviceRequest.Target.MethodName}";
            
            return new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(serviceRequest, jsonSettings), Encoding.UTF8, "application/json")
                {
                    Headers = { { GigyaHttpHeaders.Version, HttpServiceRequest.Version } }
                }
            };
        }

        // TBD: What do we do if different environment return different schemas? Should we return all of them, should we merge them?
        public Task<ServiceSchema> GetSchema()
        {
            throw new NotImplementedException();
        }
    }

    public class DeployedService : IDisposable
    {
        internal IMemoizer Memoizer { get; }
        internal ServiceSchema Schema { get; set; }
        internal RemoteHostPool LoadBalancer { get; }



        public void Dispose()
        {
            Memoizer.TryDispose();
            LoadBalancer.TryDispose();
        }
    }
}