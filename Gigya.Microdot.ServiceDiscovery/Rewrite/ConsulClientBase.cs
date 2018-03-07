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
    public abstract class ConsulClientBase: IDisposable
    {
        private bool _disposed;

        protected HttpClient HttpClient { get; private set; }

        protected ILog Log { get; }
        protected IDateTime DateTime { get; }
        protected Func<ConsulConfig> GetConfig { get; }
        protected Uri ConsulAddress { get; set; }
        protected string DataCenter { get; set; }

        protected ConsulClientBase(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;
            var address = environmentVariableProvider.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500";
            ConsulAddress = new Uri($"http://{address}");
            DataCenter = environmentVariableProvider.DataCenter;
        }

        protected async Task<ConsulResult> CallConsul(string urlCommand, CancellationToken cancellationToken)
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

                    var consulResponse = new ConsulResult { RequestLog = requestLog, StatusCode = statusCode, ResponseContent = responseContent, ResponseDateTime = DateTime.UtcNow };

                    if (statusCode == HttpStatusCode.OK)
                        consulResponse.ModifyIndex = GetConsulIndex(response);
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
                            consulResponse.ResponseContent = responseStr;
                            consulResponse.IsDeployed = false;
                        }
                        else
                            consulResponse.Error = exception;

                    }

                    return consulResponse;
                }
            }
            catch (Exception ex)
            {
                return new ConsulResult { RequestLog = requestLog, ResponseContent = responseContent, Error = ex, StatusCode = statusCode };
            }
        }


        protected static ulong? GetConsulIndex(HttpResponseMessage response)
        {
            ulong? modifyIndex = null;
            response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders);
            if (consulIndexHeaders != null && ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out var consulIndexValue))
                modifyIndex = consulIndexValue;
            return modifyIndex;
        }

        protected T TryDeserialize<T>(string response)
        {
            if (response == null)
                return default(T);
            try
            {
                return JsonConvert.DeserializeObject<T>(response);
            }
            catch
            {
                return default(T);
            }
        }

        protected void SetErrorResult(ConsulServiceState serviceState, ConsulResult result, string errorMessage)
        {
            if (result.Error == null)
                result.Error = new EnvironmentException(errorMessage);

            if (!(result.Error is TaskCanceledException))
                Log.Error("Error calling Consul", exception: result.Error, unencryptedTags: new
                {
                    ServiceName = serviceState.ServiceName,
                    ConsulAddress = ConsulAddress.ToString(),
                    consulQuery = result.RequestLog,
                    ResponseCode = result.StatusCode,
                    Content = result.ResponseContent
                });

            lock (serviceState)
            {
                serviceState.IsDeployed = true;
                serviceState.Nodes = new INode[0];
                serviceState.LastResult = result;
            }
        }

        protected void SetServiceMissingResult(ConsulServiceState serviceState, ConsulResult consulResult)
        {
            lock (serviceState)
            {
                serviceState.IsDeployed = false;
                serviceState.Nodes = new INode[0];
                serviceState.LastResult = consulResult;
            }
        }

        protected void SetConsulNodes(ServiceEntry[] consulNodes, ConsulServiceState serviceState, ConsulResult consulResult, bool filterByVersion)
        {
            lock (serviceState)
            {
                var nodes = consulNodes.Select(n => new Node(n.Node.Name, n.Service.Port, GetNodeVersion(n))).ToArray();

                serviceState.NodesOfAllVersions = nodes;
                serviceState.LastResult = consulResult;
                serviceState.IsDeployed = true;
            }
        }



        private static string GetNodeVersion(ServiceEntry node)
        {
            const string versionPrefix = "version:";
            var versionTag = node?.Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            return versionTag?.Substring(versionPrefix.Length);
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