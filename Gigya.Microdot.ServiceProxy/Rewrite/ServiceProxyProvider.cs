using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
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
        private JsonExceptionSerializer ExceptionSerializer { get; }
        private IEnvironmentVariableProvider EnvProvider { get; }

        private ConcurrentDictionary<string, DeployedService> Deployments { get; set; } = new ConcurrentDictionary<string, DeployedService>();

        public ServiceProxyProvider(string serviceName, IMemoizer memoizer, Func<DiscoveryConfig> getDiscoveryConfig,
            IHttpClientFactory httpFactory, JsonExceptionSerializer exceptionSerializer, IEnvironmentVariableProvider envProvider)
        {
            ServiceName = serviceName;
            Memoizer = memoizer;
            GetDiscoveryConfig = getDiscoveryConfig;
            HttpFactory = httpFactory;
            ExceptionSerializer = exceptionSerializer;
            EnvProvider = envProvider;
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

            Task<object> resultTask;
            INode node = TracingContext.GetHostOverride(ServiceName);
            GetDiscoveryConfig().Services.TryGetValue(ServiceName, out ServiceDiscoveryConfig config);

            async Task<object> Send()
            {
                HttpRequestMessage httpRequest = CreateHttpRequest(request, jsonSettings, node);
                string responseContent = await SendRequest(httpRequest, config, node).ConfigureAwait(false);
                return await ParseResponse(responseContent, resultReturnType, jsonSettings, httpRequest?.RequestUri.ToString()).ConfigureAwait(false);
            }

            bool isMethodCached = false;
            MethodCachingPolicyConfig cachingPolicy = null;

            if (node == null)
            {
                DeployedService targetDeployment = Route();
                isMethodCached = targetDeployment.Schema.TryFindMethod(request.Target)?.IsCached ?? false;
                cachingPolicy = config?.CachingPolicy?.Methods?[request.Target.MethodName] ?? CachingPolicyConfig.Default;
                node = targetDeployment.LoadBalancer.GetNode();
            }

            if (isMethodCached && cachingPolicy.Enabled == true)
                resultTask = (Task<object>)Memoizer.GetOrAdd(request.ComputeCacheKey(), Send, new CacheItemPolicyEx(cachingPolicy));
            else
                resultTask = Send();

            return await resultTask.ConfigureAwait(false);
        }

        private async Task<object> ParseResponse(string responseContent, Type resultReturnType, JsonSerializerSettings jsonSettings, string uri)
        {
            try
            {
                return JsonConvert.DeserializeObject(responseContent, resultReturnType, jsonSettings);
            }
            catch (JsonException ex)
            {
                throw Ex.UnparsableJsonResponse(uri, ex, responseContent);
            }
        }

        private async Task<string> SendRequest(HttpRequestMessage request, ServiceDiscoveryConfig config, INode node = null)
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
                    throw Ex.NonMicrodotHost(uri, response.StatusCode);
                }

                if (response.IsSuccessStatusCode == false)
                {
                    Exception remoteException = GetFailureResponse(responseContent, uri);

                    if (remoteException.StackTrace != null)
                        ExceptionDispatchInfo.Capture(remoteException).Throw();

                    throw remoteException;
                }

                return responseContent;
            }
            catch (HttpRequestException ex)
            {
                //node?.ReportFailure(ex);
                throw Ex.BadHttpResponse(uri, ex);
            }
            catch (TaskCanceledException ex)
            {
                throw Ex.Timeout(uri, ex, http.Timeout);
            }
        }

        private Exception GetFailureResponse(string responseContent, string uri)
        {
            Exception remoteException;

            try
            {
                remoteException = ExceptionSerializer.Deserialize(responseContent);
            }
            catch (Exception ex)
            {
                return Ex.UnparsableFailureResponse(uri, ex, responseContent);
            }

            #pragma warning disable 618

            // Unwrap obsolete wrapper which is still used by old servers.
            if (remoteException is UnhandledException)
                remoteException = remoteException.InnerException;

            #pragma warning restore 618

            if (remoteException is RequestException == false && remoteException is EnvironmentException == false)
                remoteException = Ex.FailureResponse(uri, remoteException);

            return remoteException;
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
            string[] environmentCandidates = { EnvProvider.DeploymentEnvironment, "prod" };
            DeployedService selectedDeployment = null;

            foreach (string env in environmentCandidates)
            {
                Deployments.TryGetValue(env, out selectedDeployment);
                
                if (selectedDeployment != null)
                    break;
            }

            if (selectedDeployment == null)
                throw Ex.RoutingFailed(ServiceName, environmentCandidates, Deployments.Keys.ToArray());

            return selectedDeployment;
        }

        // TBD: What do we do if different environment return different schemas? Should we return all of them, should we merge them?
        public async Task<ServiceSchema> GetSchema()
        {
            return Route().Schema;
        }
    }
}