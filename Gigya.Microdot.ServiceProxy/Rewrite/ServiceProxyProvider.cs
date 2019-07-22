using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Rewrite;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
	/// <summary>
	/// This is a beta version. Please do not use it until it's ready
	/// </summary>
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
            
            var hostOverride = TracingContext.GetHostOverride(ServiceName);

            if (hostOverride != null)
            {
                var httpRequest = CreateHttpRequest(request, jsonSettings, hostOverride);
            }
            else
            {
                
            }

            return null;
        }

        private HttpRequestMessage CreateHttpRequest(HttpServiceRequest request, JsonSerializerSettings jsonSettings, HostOverride node)
        {
            string uri = $"{(HttpSettings.UseHttps ? "https" : "http")}://{node.Host}:{node.Port ?? HttpSettings.BasePort}/{ServiceName}.{request.Target.MethodName}";
            
            return new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(request, jsonSettings), Encoding.UTF8, "application/json")
                {
                    Headers = { { GigyaHttpHeaders.ProtocolVersion, HttpServiceRequest.ProtocolVersion } }
                }
            };
        }

        // TBD: What do we do if different environment return different schemas? Should we return all of them, should we merge them?
        public Task<ServiceSchema> GetSchema()
        {
            throw new NotImplementedException();
        }
    }
}