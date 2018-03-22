using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.HttpService.Schema;
using Gigya.Microdot.SharedLogic.Rewrite;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
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

        private IMemoizer Memoizer { get; }
        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }
        private IHttpClientFactory HttpFactory { get; }

        private ConcurrentDictionary<string, DeployedService> Deployments { get; set; }

        public ServiceProxyProvider(string serviceName, IMemoizer memoizer, Func<DiscoveryConfig> getDiscoveryConfig, IHttpClientFactory httpFactory)
        {
            ServiceName = serviceName;
            Memoizer = memoizer;
            GetDiscoveryConfig = getDiscoveryConfig;
            HttpFactory = httpFactory;
        }

        public object Invoke(MethodInfo targetMethod, object[] args)
        {
            // TODO: Add caching to this step to prevent using reflection every call
            Type resultReturnType = targetMethod.ReturnType.GetGenericArguments().SingleOrDefault() ?? typeof(object);
            var request = new HttpServiceRequest(targetMethod, args);

            return TaskConverter.ToStronglyTypedTask(Invoke(request, resultReturnType), resultReturnType);
        }

        public async Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (resultReturnType == null)
                throw new ArgumentNullException(nameof(resultReturnType));

            Task resultTask;
            HostOverride hostOverride = TracingContext.GetHostOverride(ServiceName);
            GetDiscoveryConfig().Services.TryGetValue(ServiceName, out ServiceDiscoveryConfig config);

            if (hostOverride != null)
            {
                HttpRequestMessage httpRequest = CreateHttpRequest(request, jsonSettings, hostOverride);
                resultTask = SendRequest(httpRequest, config);
            }
            else
            {
                DeployedService targetDeployment = Route();
                bool isMethodCached = targetDeployment.Schema.TryFindMethod(request.Target)?.IsCached ?? false;
                MethodCachingPolicyConfig cachingPolicy = config?.CachingPolicy?.Methods?[request.Target.MethodName] ?? CachingPolicyConfig.Default;
                
                Task Send()
                {
                    INode node = targetDeployment.LoadBalancer.GetNode();
                    return SendRequest(CreateHttpRequest(request, jsonSettings, node), config, node);
                }

                if (isMethodCached && cachingPolicy.Enabled == true)
                    resultTask = Memoizer.GetOrAdd(request.ComputeCacheKey(), Send, new CacheItemPolicyEx(cachingPolicy));
                else
                    resultTask = Send();
            }

            await resultTask.ConfigureAwait(false);

            return null;
        }


        private async Task SendRequest(HttpRequestMessage request, ServiceDiscoveryConfig config, INode node = null)
        {
            HttpClient http = HttpFactory.GetClient(config.UseHttpsOverride ?? HttpSettings.UseHttps, config.RequestTimeout);
            string uri = request.RequestUri.ToString();

            try
            {
                HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                if (response.Headers.Contains(GigyaHttpHeaders.ServerHostname) == false && response.Headers.Contains(GigyaHttpHeaders.ProtocolVersion) == false)
                {
                    //node?.ReportFailure(ex);
                    throw RemoteServiceException.NonMicrodotHost(uri, response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                //node?.ReportFailure(ex);
            }
            catch (TaskCanceledException ex)
            {
                throw RemoteServiceException.Timeout(uri, ex, http.Timeout);
            }
        }

        private HttpRequestMessage CreateHttpRequest(HttpServiceRequest request, JsonSerializerSettings jsonSettings, INode node)
        {
            // The URL is only for a nice experience in Fiddler, it's never parsed/used for anything.
            string uri = $"{(HttpSettings.UseHttps ? "https" : "http")}://{node.Hostname}:{node.Port ?? HttpSettings.BasePort}/{ServiceName}.{request.Target.MethodName}";
            
            return new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(request, jsonSettings ?? JsonSettings), Encoding.UTF8, "application/json")
                {
                    Headers = { { GigyaHttpHeaders.ProtocolVersion, HttpServiceRequest.ProtocolVersion } }
                }
            };
        }

        private DeployedService Route()
        {

        }

        // TBD: What do we do if different environment return different schemas? Should we return all of them, should we merge them?
        public Task<ServiceSchema> GetSchema()
        {
            throw new NotImplementedException();
        }
    }
}