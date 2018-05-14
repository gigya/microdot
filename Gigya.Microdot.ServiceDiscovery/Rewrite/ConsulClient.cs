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
        private int _disposed = 0;

        protected HttpClient HttpClient { get; private set; }

        protected ILog Log { get; }
        protected IDateTime DateTime { get; }
        protected Func<ConsulConfig> GetConfig { get; }
        public Uri ConsulAddress => HttpClient.BaseAddress;


        public ConsulClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            if (!string.IsNullOrEmpty(environmentVariableProvider.ConsulAddress))
                HttpClient = new HttpClient { BaseAddress = new Uri($"http://{environmentVariableProvider.ConsulAddress}") };
            else
                HttpClient = new HttpClient { BaseAddress = new Uri($"http://{CurrentApplicationInfo.HostName}:8500") };

            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;
        }


        public async Task<ConsulResult<TResponse>> Call<TResponse>(string commandPath, CancellationToken cancellationToken)
        {
            if (_disposed > 0)
                throw new ObjectDisposedException(nameof(ConsulClient));

            var timeout = GetConfig().HttpTimeout;

            if (HttpClient?.Timeout != timeout)
                HttpClient = new HttpClient {BaseAddress = ConsulAddress, Timeout = timeout};

            string responseContent = null;
            var consulResult = new ConsulResult<TResponse> {ConsulAddress = ConsulAddress.ToString(), CommandPath = commandPath};

            try
            {
                using (new TraceContext($"ConsulClient call ({commandPath})"))
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
                if (consulResult.StatusCode == HttpStatusCode.NotFound ||
                    responseContent?.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    consulResult.IsUndeployed = true;
                    consulResult.ResponseContent = responseContent;
                }
                else
                {
                    consulResult.ConsulResponseError();
                }
            }

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