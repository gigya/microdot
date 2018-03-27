using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
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
        private bool _disposed;

        protected HttpClient HttpClient { get; private set; }

        protected ILog Log { get; }
        protected IDateTime DateTime { get; }
        protected Func<ConsulConfig> GetConfig { get; }
        public Uri ConsulAddress { get; set; }
        protected string DataCenter { get; set; }

        public ConsulClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;

            var address = $"{CurrentApplicationInfo.HostName}:8500";
            if (!string.IsNullOrEmpty(environmentVariableProvider.ConsulAddress))
                address = environmentVariableProvider.ConsulAddress;

            ConsulAddress = new Uri($"http://{address}");
            DataCenter = environmentVariableProvider.DataCenter;
        }

        public async Task<ConsulResult<TResponse>> Call<TResponse>(string urlCommand, CancellationToken cancellationToken)
        {
            var timeout = GetConfig().HttpTimeout;
            ulong? modifyIndex = 0;
            string requestLog = string.Empty;
            string responseContent = null;
            HttpStatusCode? statusCode = null;

            try
            {
                if (HttpClient == null)
                    HttpClient = new HttpClient { BaseAddress = ConsulAddress };

                requestLog = HttpClient.BaseAddress + urlCommand;
                using (var timeoutcancellationToken = new CancellationTokenSource(timeout))
                using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutcancellationToken.Token))
                using (var response = await HttpClient.GetAsync(urlCommand, HttpCompletionOption.ResponseContentRead, cancellationSource.Token).ConfigureAwait(false))
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    statusCode = response.StatusCode;
                    Log.Debug(x => x("Response received from Consul", unencryptedTags: new { ConsulAddress, requestLog = urlCommand, responseCode = statusCode, responseContent }));

                    var consulResult = new ConsulResult<TResponse> { RequestLog = requestLog, StatusCode = statusCode, ResponseContent = responseContent, ResponseDateTime = DateTime.UtcNow };

                    if (statusCode == HttpStatusCode.OK)
                        consulResult.ModifyIndex = GetConsulIndex(response);
                    else
                    {
                        var exception = new EnvironmentException("Consul response not OK",
                            unencrypted: new Tags
                            {
                                {"ConsulAddress", ConsulAddress.ToString()},
                                {"ConsulQuery", urlCommand},
                                {"ResponseCode", statusCode.ToString()},
                                {"Content", responseContent}
                            });

                        if (statusCode == HttpStatusCode.NotFound || responseContent.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var responseStr = string.IsNullOrEmpty(responseContent) ? "404 NotFound" : responseContent;
                            consulResult.ResponseContent = responseStr;
                            consulResult.IsDeployed = false;
                        }
                        else
                            consulResult.Error = exception;

                    }
                    if (consulResult.Success)
                    {
                        try
                        {
                            consulResult.Response = JsonConvert.DeserializeObject<TResponse>(responseContent);
                        }
                        catch (Exception ex)
                        {
                            consulResult.Error = new EnvironmentException("Error serializing Consul response",
                                innerException: ex, unencrypted: new Tags
                                {
                                    {"requestLog", requestLog},
                                    {"responseContent", responseContent},
                                    {"responseType", typeof(TResponse).Name}
                                });
                        }
                    }
                    return consulResult;
                }
            }
            catch (Exception ex)
            {
                return new ConsulResult<TResponse> { RequestLog = requestLog, ResponseContent = responseContent, Error = ex, StatusCode = statusCode };
            }
        }


        private static ulong? GetConsulIndex(HttpResponseMessage response)
        {
            ulong? modifyIndex = null;
            response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders);
            if (consulIndexHeaders != null && ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out var consulIndexValue))
                modifyIndex = consulIndexValue;
            return modifyIndex;
        }

        private static string GetNodeVersion(ServiceEntry node)
        {
            const string versionPrefix = "version:";
            var versionTag = node?.Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            return versionTag?.Substring(versionPrefix.Length);
        }



        public Node[] ReadConsulNodes(ServiceEntry[] consulNodes)
        {
            return consulNodes.Select(n => new Node(n.Node.Name, n.Service.Port, GetNodeVersion(n))).ToArray();
        }

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