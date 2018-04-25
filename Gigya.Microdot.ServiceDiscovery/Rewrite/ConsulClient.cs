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
    public class ConsulClient: IDisposable
    {
        private bool _disposed;

        protected HttpClient HttpClient { get; private set; }

        protected ILog Log { get; }
        protected IDateTime DateTime { get; }
        protected Func<ConsulConfig> GetConfig { get; }
        public Uri ConsulAddress { get; set; }
        protected string DataCenter { get; set; }

        public ConsulClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            if (!string.IsNullOrEmpty(environmentVariableProvider.ConsulAddress))
                ConsulAddress = new Uri($"http://{environmentVariableProvider.ConsulAddress}");
            else
                ConsulAddress = new Uri($"http://{CurrentApplicationInfo.HostName}:8500");

            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            HttpClient = new HttpClient { BaseAddress = ConsulAddress };
        }

        public async Task<ConsulResult<TResponse>> Call<TResponse>(string commandPath, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConsulClient));

            using (var timeoutCancellationToken = new CancellationTokenSource(GetConfig().HttpTimeout))
            using (CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationToken.Token))
            {
                string responseContent = null;
                var consulResult = new ConsulResult<TResponse> { ConsulAddress = ConsulAddress.ToString(), CommandPath = commandPath};

                try
                {
                    HttpResponseMessage response = await HttpClient.GetAsync(commandPath, HttpCompletionOption.ResponseContentRead, cancellationSource.Token).ConfigureAwait(false);

                    using (response)
                    {
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        consulResult.StatusCode = response.StatusCode;
                        consulResult.ResponseContent = responseContent;
                        consulResult.ResponseDateTime = DateTime.UtcNow;
                        consulResult.ModifyIndex = GetConsulIndex(response);
                    }
                }
                catch (Exception ex)
                {
                    consulResult.ConsulUnreachable(ex);
                    return consulResult;
                }

                Log.Debug(x => x("Response received from Consul", unencryptedTags: new { consulAddress = ConsulAddress, commandPath, responseCode = consulResult.StatusCode, responseContent }));


                if (consulResult.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        consulResult.Response = JsonConvert.DeserializeObject<TResponse>(responseContent);
                    }
                    catch (Exception ex)
                    {
                        consulResult.UnparsableConsulResponse(ex);
                    }
                }
                else
                {
                    if (consulResult.StatusCode == HttpStatusCode.NotFound || responseContent?.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase) == true)
                    {
                        consulResult.IsDeployed = false;
                        consulResult.ResponseContent = string.IsNullOrEmpty(responseContent) ? "404 NotFound" : responseContent;
                    }
                    else
                    {
                        consulResult.ConsulResponseError();
                    }
                }
                
                return consulResult;
            }
        }


        private static ulong? GetConsulIndex(HttpResponseMessage response)
        {
            ulong? modifyIndex = null;
            response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders);
            if (consulIndexHeaders != null && ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out ulong consulIndexValue))
                modifyIndex = consulIndexValue;
            return modifyIndex;
        }


        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
                HttpClient?.Dispose();

            _disposed = true;
        }

    }
}