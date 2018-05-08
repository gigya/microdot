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
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
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

        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }
        private Func<IMemoizer> MemoizerFactory { get; }
        private IHttpClientFactory HttpFactory { get; }
        private JsonExceptionSerializer ExceptionSerializer { get; }
        private IEnvironmentVariableProvider EnvProvider { get; }
        private IDiscoveryFactory DiscoveryFactory { get; }
        private ConcurrentDictionary<string, DeployedService> Deployments { get; } = new ConcurrentDictionary<string, DeployedService>();
        private ConcurrentDictionary<MethodInfo, Type> ResultReturnTypeCache { get; } = new ConcurrentDictionary<MethodInfo, Type>();
        private string GetBaseUri(INode node, bool useHttps) => $"{(useHttps ? "https" : "http")}://{node.Hostname}:{node.Port ?? HttpSettings.BasePort}/";


        public ServiceProxyProvider(string serviceName, HttpServiceAttribute httpSettings, Func<DiscoveryConfig> getDiscoveryConfig, Func<IMemoizer> memoizerFactory,
            IHttpClientFactory httpFactory, JsonExceptionSerializer exceptionSerializer, IEnvironmentVariableProvider envProvider,
            IDiscoveryFactory discoveryFactory)
        {
            ServiceName = serviceName;
            HttpSettings = httpSettings;
            GetDiscoveryConfig = getDiscoveryConfig;
            MemoizerFactory = memoizerFactory;
            HttpFactory = httpFactory;
            ExceptionSerializer = exceptionSerializer;
            EnvProvider = envProvider;
            DiscoveryFactory = discoveryFactory;
        }

        private ServiceDiscoveryConfig GetConfig()
        {
            GetDiscoveryConfig().Services.TryGetValue(ServiceName, out ServiceDiscoveryConfig config);
            return config;
        }

        public object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type resultReturnType = ResultReturnTypeCache.GetOrAdd(targetMethod, m => m.ReturnType.GetGenericArguments().SingleOrDefault() ?? typeof(object));
            var request = new HttpServiceRequest(targetMethod, args);

            return TaskConverter.ToStronglyTypedTask(Invoke(request, resultReturnType), resultReturnType);
        }

        public async Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (resultReturnType == null)
                throw new ArgumentNullException(nameof(resultReturnType));

            jsonSettings = jsonSettings ?? JsonSettings;
            Task<object> resultTask;
            INode node = TracingContext.GetHostOverride(ServiceName);
            ServiceDiscoveryConfig config = GetConfig();

            async Task<object> Send()
            {
                bool useHttps = config?.UseHttpsOverride ?? HttpSettings.UseHttps;
                HttpRequestMessage httpRequest = CreateHttpRequest(request, jsonSettings, node, useHttps);
                string responseContent = await SendRequest(httpRequest, config, useHttps, node).ConfigureAwait(false);
                return ParseResponse(responseContent, resultReturnType, jsonSettings, httpRequest?.RequestUri.ToString());
            }

            bool isMethodCached = false;
            MethodCachingPolicyConfig cachingPolicy = null;
            DeployedService targetDeployment = await Route(config).ConfigureAwait(false);

            if (node == null)
            {
                isMethodCached = targetDeployment.Schema.TryFindMethod(request.Target)?.IsCached ?? false;
                cachingPolicy = config?.CachingPolicy?.Methods?[request.Target.MethodName] ?? CachingPolicyConfig.Default;
                node = targetDeployment.LoadBalancer.GetNode();
            }

            if (isMethodCached && cachingPolicy.Enabled == true)
                resultTask = (Task<object>)targetDeployment.Memoizer.GetOrAdd(request.ComputeCacheKey(), Send, resultReturnType, new CacheItemPolicyEx(cachingPolicy));
            else
                resultTask = Send();

            return await resultTask.ConfigureAwait(false);
        }

        private object ParseResponse(string responseContent, Type resultReturnType, JsonSerializerSettings jsonSettings, string uri)
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

        private async Task<string> SendRequest(HttpRequestMessage request, ServiceDiscoveryConfig config, bool useHttps, INode node = null)
        {
            HttpClient http = HttpFactory.GetClient(useHttps, config.RequestTimeout);
            string uri = request.RequestUri.ToString();

            try
            {
                HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                if (response.Headers.Contains(GigyaHttpHeaders.ServerHostname) == false && response.Headers.Contains(GigyaHttpHeaders.ProtocolVersion) == false)
                {
                    var ex = Ex.NonMicrodotHost(uri, response.StatusCode);
                    if (node is IMonitoredNode mNode)
                        mNode.ReportUnreachable(ex);
                    throw ex;
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
            catch (HttpRequestException httpEx)
            {
                var ex = Ex.BadHttpResponse(uri, httpEx);
                if (node is IMonitoredNode mNode)
                    mNode.ReportUnreachable(ex);
                throw ex;
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

        private HttpRequestMessage CreateHttpRequest(HttpServiceRequest request, JsonSerializerSettings jsonSettings, INode node, bool useHttps)
        {
            // The URL is only for a nice experience in Fiddler, it's never parsed/used for anything.
            string uri = $"{GetBaseUri(node, useHttps)}/{ServiceName}.{request.Target.MethodName}";
            
            return new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(request, jsonSettings ?? JsonSettings), Encoding.UTF8, "application/json")
                {
                    Headers = { { GigyaHttpHeaders.ProtocolVersion, HttpServiceRequest.ProtocolVersion } }
                }
            };
        }

        private async Task<DeployedService> Route(ServiceDiscoveryConfig config)
        {
            string[] environmentCandidates = { EnvProvider.DeploymentEnvironment, "prod", "" };
            DeployedService selectedDeployment = null;

            foreach (string env in environmentCandidates)
            {
                selectedDeployment = await GetDeployment(env, config).ConfigureAwait(false);
                
                if (selectedDeployment != null)
                    break;
            }

            if (selectedDeployment == null)
            {
                var allAvailableDeployments = Deployments.Where(d => d.Value.LoadBalancer?.WasUndeployed == false)
                    .Select(d => d.Key)
                    .ToArray();

                throw Ex.RoutingFailed(ServiceName, environmentCandidates, allAvailableDeployments);
            }

            return selectedDeployment;
        }

        private async Task<DeployedService> GetDeployment(string env, ServiceDiscoveryConfig config)
        {
            DeployedService deployment = Deployments.GetOrAdd(env, k => new DeployedService());

            using (await deployment.Lock.LockAsync().ConfigureAwait(false))
            {
                if (deployment.LoadBalancer?.WasUndeployed == true)
                {
                    deployment.LoadBalancer.Dispose();
                    deployment.LoadBalancer = null;
                }

                if (deployment.LoadBalancer == null)
                    deployment.LoadBalancer = await DiscoveryFactory.TryCreateLoadBalancer(new DeploymentIdentifier(ServiceName, env), new ReachabilityCheck(IsReachable)).ConfigureAwait(false);

                if (deployment.LoadBalancer == null)
                    return null;

                if (deployment.Schema == null)
                    await RefreshSchema(deployment, config).ConfigureAwait(false);

                if (deployment.Memoizer == null)
                    deployment.Memoizer = MemoizerFactory();
            }

            return deployment;
        }

        private async Task<bool> IsReachable(INode node)
        {
            try
            {
                bool useHttps = GetConfig()?.UseHttpsOverride ?? HttpSettings.UseHttps;
                HttpClient http = HttpFactory.GetClient(useHttps, TimeSpan.FromSeconds(30));
                string uri = GetBaseUri(node, useHttps);
                HttpResponseMessage response = await http.GetAsync(uri, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                return response.Headers.Contains(GigyaHttpHeaders.ServerHostname);
            }
            catch
            {
                return false;
            }
        }

        private async Task RefreshSchema(DeployedService deployment, ServiceDiscoveryConfig config)
        {
            bool useHttps = config?.UseHttpsOverride ?? HttpSettings.UseHttps;
            IMonitoredNode monitoredNode = deployment.LoadBalancer.GetNode();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{GetBaseUri(monitoredNode, useHttps)}/schema");
            string responseContent = await SendRequest(request, config, useHttps, monitoredNode).ConfigureAwait(false);
            deployment.Schema = JsonConvert.DeserializeObject<ServiceSchema>(responseContent, JsonSettings);
        }

        // TBD: What do we do if different environment return different schemas? Should we return all of them, should we merge them?
        public async Task<ServiceSchema> GetSchema()
        {
            return (await Route(GetConfig()).ConfigureAwait(false)).Schema;
        }
    }
}