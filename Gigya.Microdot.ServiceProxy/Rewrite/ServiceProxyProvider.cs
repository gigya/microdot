using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection.DispatchProxy;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Rewrite;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
	public class ServiceProxyProvider : IServiceProxyProvider
    {
        public IProxyable ProxyProvider { get; set; }

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

        private Func<string, ServiceProxy.IServiceProxyProvider> CreateServiceProxyProvider { get; }
        private Func<object, string, ICachingProxyProvider> CreateCachingProxyProvider { get; }

        public ServiceProxyProvider(Type serviceType, IMetadataProvider metadataProvider, Func<string, ServiceProxy.IServiceProxyProvider> createServiceProxyProvider, Func<object, string, ICachingProxyProvider> createCachingProxyProvider)
        {
            ServiceName = serviceType.GetServiceName();
            CreateServiceProxyProvider = createServiceProxyProvider;
            CreateCachingProxyProvider = createCachingProxyProvider;

            ProxyProvider = CreateInnerProvider(serviceType);

            if (metadataProvider.HasCachedMethods(serviceType))
            {
                ProxyProvider = CreateCachingProxyProvider(ProxyProvider.ToProxy(serviceType), ServiceName);
            }
        }

        private IProxyable CreateInnerProvider(Type serviceType)
        {
            var attribute = (HttpServiceAttribute)Attribute.GetCustomAttribute(serviceType, typeof(HttpServiceAttribute));

            if (attribute == null)
                throw new ProgrammaticException("The specified service interface type is not decorated with HttpServiceAttribute.", unencrypted: new Tags { { "interfaceName", serviceType.Name } });

            var innerProvider = CreateServiceProxyProvider(serviceType.GetServiceName());
            innerProvider.UseHttpsDefault = attribute.UseHttps;

            if (innerProvider.DefaultPort == null)
            {
                innerProvider.DefaultPort = attribute.BasePort + (int)PortOffsets.Http;
            }

            return innerProvider;
        }        

        public object Invoke(MethodInfo targetMethod, object[] args)
        {
            return ProxyProvider.Invoke(targetMethod, args);
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
            string uri = $"{(HttpSettings.UseHttps ? "https" : "http")}://{node.Hostname}:{node.Port ?? HttpSettings.BasePort}/{ServiceName}.{request.Target.MethodName}";
            
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