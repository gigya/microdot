using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{

    internal class ConsulClient : IDisposable
    {
        private ILog Log { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private Uri ConsulAddress => _httpClient.BaseAddress;        
        private string Zone { get; }
        private HttpClient _httpClient;
        private int _disposed = 0;



        public ConsulClient(ILog log, IEnvironment environment, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            Zone = environment.Zone;
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;

            if (environment.ConsulAddress != null)
                _httpClient = new HttpClient { BaseAddress = new Uri($"http://{environment.ConsulAddress}") };
            else
                _httpClient = new HttpClient { BaseAddress = new Uri($"http://{CurrentApplicationInfo.HostName}:8500") };
        }


        public async Task<ConsulResponse<ConsulNode[]>> GetHealthyNodes(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/health/service/{deploymentIdentifier.GetConsulServiceName()}?dc={deploymentIdentifier.Zone}&passing&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<ConsulNode[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var serviceEntries = JsonConvert.DeserializeObject<ServiceEntry[]>(response.ResponseContent);
                    response.Result = serviceEntries.Select(ToNode).ToArray();
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.Error == null)
                response.ConsulResponseError();

            return response;
        }


        public async Task<ConsulResponse<T>> GetKey<T>(ulong modifyIndex, string folder, string key,
            string zone = null, CancellationToken cancellationToken=default(CancellationToken)) where T: class
        {
            T result = null;
            string urlCommand = $"v1/kv/{folder}/{key}?dc={zone ?? Zone}&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<KeyValueResponse[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var keyValues = JsonConvert.DeserializeObject<KeyValueResponse[]>(response.ResponseContent);
                    result = keyValues.SingleOrDefault()?.DecodeValue<T>();                    
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.Error == null)
                response.ConsulResponseError();

            return response.SetResult(result);
        }

        public async Task<ConsulResponse<string>> GetDeploymentVersion(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            string version = null;
            var response = await GetKey<ServiceKeyValue>(modifyIndex, "service", deploymentIdentifier.GetConsulServiceName(), deploymentIdentifier.Zone, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.IsUndeployed = true;
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                version = response.Result?.Version;
                response.IsUndeployed = false;
            }
            else if (response.Error == null)
                response.ConsulResponseError();
            return response.SetResult(version);
        }



        public async Task<ConsulResponse<string[]>> GetAllKeys(ulong modifyIndex, string folder, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/kv/{folder}?dc={Zone}&keys&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<string[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var fullKeyNames = JsonConvert.DeserializeObject<string[]>(response.ResponseContent);
                    var keyNames = fullKeyNames.Select(s => s.Substring($"{folder}/".Length)).ToArray();
                    response.Result = keyNames;
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.Error == null)
                response.ConsulResponseError();

            return response;
        }


        public Task<ConsulResponse<string[]>> GetAllServices(ulong modifyIndex, CancellationToken cancellationToken)
        {
            return GetAllKeys(modifyIndex, "service", cancellationToken);
        }



        private async Task<ConsulResponse<T>> Call<T>(string commandPath, CancellationToken cancellationToken)
        {
            if (_disposed > 0)
                throw new ObjectDisposedException(nameof(ConsulClient));

            var timeout = GetConfig().HttpTaskTimeout;

            if (_httpClient.Timeout != timeout)
                _httpClient = new HttpClient { BaseAddress = ConsulAddress, Timeout = timeout };

            string responseContent = null;
            var consulResult = new ConsulResponse<T> { ConsulAddress = ConsulAddress.ToString(), CommandPath = commandPath };

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(commandPath, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

                using (response)
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    consulResult.StatusCode = response.StatusCode;
                    consulResult.ResponseContent = responseContent;
                    consulResult.ResponseDateTime = DateTime.UtcNow;
                    consulResult.ModifyIndex = TryGetConsulIndex(response);
                }
            }
            catch (Exception ex)
            {
                consulResult.ConsulUnreachable(ex);
                return consulResult;
            }

            Log.Debug(x => x("Response received from Consul",
                unencryptedTags: new
                {
                    consulAddress = ConsulAddress,
                    commandPath,
                    responseCode = consulResult.StatusCode,
                    responseContent
                }));

            return consulResult;
        }

        private ConsulNode ToNode(ServiceEntry serviceEntry)
        {
            const string versionPrefix = "version:";
            string versionTag = serviceEntry.Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            string version = versionTag?.Substring(versionPrefix.Length);

            return new ConsulNode(serviceEntry.Node.Name, serviceEntry.Service?.Port, version);
        }



        private static ulong? TryGetConsulIndex(HttpResponseMessage response)
        {
            response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders);
            if (consulIndexHeaders != null && ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out ulong consulIndexValue))
                return consulIndexValue;
            else return null;
        }



        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            _httpClient.Dispose();
        }
    }
}