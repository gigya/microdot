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
using Gigya.Microdot.SharedLogic.Rewrite;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulClient: IDisposable
    {
        private int _disposed = 0;

        private HttpClient HttpClient { get; set; }

        private ILog Log { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private Uri ConsulAddress => HttpClient.BaseAddress;


        private string DataCenter { get; }

        public ConsulClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            if (!string.IsNullOrEmpty(environmentVariableProvider.ConsulAddress))
                HttpClient = new HttpClient { BaseAddress = new Uri($"http://{environmentVariableProvider.ConsulAddress}") };
            else
                HttpClient = new HttpClient { BaseAddress = new Uri($"http://{CurrentApplicationInfo.HostName}:8500") };

            DataCenter = environmentVariableProvider.DataCenter;
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;
        }

        public async Task<ConsulResponse<Node[]>> GetHealthyNodes(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/health/service/{deploymentIdentifier}?dc={DataCenter}&passing&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<Node[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var serviceEntries = JsonConvert.DeserializeObject<ServiceEntry[]>(response.ResponseContent);
                    response.Result = serviceEntries.Select(e => e.ToNode()).ToArray();
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else
                response.ConsulResponseError();

            return response;
        }

        public async Task<ConsulResponse<string>> GetDeploymentVersion(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/kv/service/{deploymentIdentifier}?dc={DataCenter}&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<string>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.IsUndeployed = true;
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var keyValues = JsonConvert.DeserializeObject<KeyValueResponse[]>(response.ResponseContent);
                    response.Result = keyValues.SingleOrDefault()?.TryDecodeValue()?.Version;
                    response.IsUndeployed = false;
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else
                response.ConsulResponseError();

            return response;
        }

        public async Task<ConsulResponse<string[]>> GetAllServices(ulong modifyIndex, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/kv/service?dc={DataCenter}&keys&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<string[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {   
                    var fullServiceNames = JsonConvert.DeserializeObject<string[]>(response.ResponseContent);                    
                    var serviceNames = fullServiceNames.Select(s => s.Substring("service/".Length)).ToArray();
                    response.Result = serviceNames;
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else
                response.ConsulResponseError();

            return response;
        }

        public async Task<ConsulResponse<INode[]>> GetNodesByQuery(DeploymentIdentifier deploymentIdentifier, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/query/{deploymentIdentifier}/execute?dc={DataCenter}";
            var response = await Call<INode[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var result = JsonConvert.DeserializeObject<ConsulQueryExecuteResponse>(response.ResponseContent);
                    response.Result = result.Nodes.Select(n => n.ToNode()).ToArray<INode>();
                    response.IsUndeployed = false;
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.ResponseContent.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase))
            {
                response.IsUndeployed = true;
            }
            else
                response.ConsulResponseError();

            return response;
        }

        private async Task<ConsulResponse<T>> Call<T>(string commandPath, CancellationToken cancellationToken)
        {
            if (_disposed > 0)
                throw new ObjectDisposedException(nameof(ConsulClient));

            var timeout = GetConfig().HttpTimeout;

            if (HttpClient?.Timeout != timeout)
                HttpClient = new HttpClient {BaseAddress = ConsulAddress, Timeout = timeout};

            string responseContent = null;
            var consulResult = new ConsulResponse<T> {ConsulAddress = ConsulAddress.ToString(), CommandPath = commandPath};

            try
            {
                HttpResponseMessage response = await HttpClient.GetAsync(commandPath, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Increment(ref _disposed) != 1) // is it the right place?
                return;

            if (disposing)
                HttpClient?.Dispose();
        }

    }
}