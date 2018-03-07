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
    public sealed class ConsulClient: IConsulClient, IDisposable
    {
        private HttpClient _httpClient;

        private ILog Log { get; }
        public IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private Uri ConsulAddress { get; set; }
        public string DataCenter { get; set; }


        private CancellationTokenSource ShutdownToken { get; }

        /// <summary>
        /// Current ModifyIndex of get-all-keys api on Consul (/v1/kv/service?keys)
        /// </summary>
        private ulong _allKeysModifyIndex = 0;

        /// <summary>
        /// Result of all keys on Consul
        /// </summary>
        private string[] _allKeys;

        private Task _getAllKeys;

        public ConsulClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;
            var address = environmentVariableProvider.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500";
            ConsulAddress = new Uri($"http://{address}");
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();
            _getAllKeys = GetAllKeys();
        }

        private async Task GetAllKeys()
        {
            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand =
                $"v1/kv/service?dc={DataCenter}&keys&index={_allKeysModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _allKeysModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                _allKeys = TryDeserialize<string[]>(response.ResponseContent);
            }
            else
            {
                Log.Warn("Error calling Consul all-keys", unencryptedTags: new
                {
                    requestLog = urlCommand,
                    consulResponse = response.ResponseContent,
                    ConsulAddress
                });
                await DateTime.Delay(config.ErrorRetryInterval).ConfigureAwait(false);
            }

            if (!ShutdownToken.IsCancellationRequested)
                _getAllKeys = GetAllKeys();
        }

        public async Task LoadNodes(ConsulServiceState serviceState)
        {
            InitGetAllKeys();
            await WaitIfErrorOccuredOnPreviousCall(serviceState).ConfigureAwait(false);

            if (!serviceState.IsDeployed)
                return;

            if (serviceState.ActiveVersion == null)
                await LoadServiceVersion(serviceState).ConfigureAwait(false);

            if (serviceState.NodesLoading == null || serviceState.NodesLoading.IsCompleted)
                serviceState.NodesLoading = LoadNodesByHealth(serviceState);
            if (serviceState.VersionLoading == null || serviceState.VersionLoading.IsCompleted)
                serviceState.VersionLoading = LoadServiceVersion(serviceState);

            await Task.WhenAny(serviceState.NodesLoading, serviceState.VersionLoading);
        }

        private async Task LoadServiceVersion(ConsulServiceState serviceState)
        {
            InitGetAllKeys();
            await WaitIfErrorOccuredOnPreviousCall(serviceState).ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/kv/service/{serviceState.ServiceName}?dc={DataCenter}&index={serviceState.VersionModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, serviceState.ShutdownToken).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                serviceState.VersionModifyIndex = response.ModifyIndex.Value;

            if (response.IsDeployed == false)
            {
                var serviceExists = await SearchServiceInAllKeys(serviceState).ConfigureAwait(false);
                if (serviceExists)
                {
                    await ReloadServiceVersion(serviceState).ConfigureAwait(false);
                    return;
                }
                else
                {
                    SetServiceMissingResult(serviceState, response);
                    return;
                }
            }
            else if (response.Success)
            {
                var keyValue = TryDeserialize<KeyValueResponse[]>(response.ResponseContent);
                var version = keyValue?.SingleOrDefault()?.TryDecodeValue()?.Version;

                if (version != null)
                {
                    lock (serviceState)
                    {
                        serviceState.ActiveVersion = version;
                        serviceState.IsDeployed = true;
                    }
                    return;
                }
            }
            SetErrorResult(serviceState, response, "Cannot extract service's active version from Consul response");
        }

        private void InitGetAllKeys()
        {
            if (_getAllKeys == null)
                _getAllKeys = GetAllKeys();
        }

        private Task ReloadServiceVersion(ConsulServiceState serviceState)
        {
            serviceState.VersionModifyIndex = 0;
            return LoadServiceVersion(serviceState);
        }

        private async Task<bool> SearchServiceInAllKeys(ConsulServiceState serviceState)
        {
            if (_allKeys == null)
            {
                InitGetAllKeys();
                await _getAllKeys.ConfigureAwait(false);
            }

            var serviceNameMatchByCase = _allKeys?.FirstOrDefault(s =>
                    s.Equals($"service/{serviceState.ServiceName}", StringComparison.InvariantCultureIgnoreCase))
                    ?.Substring("service/".Length);

            var serviceExists = serviceNameMatchByCase != null;

            if (!serviceExists)
                return false;

            if (serviceState.ServiceName != serviceNameMatchByCase)
            {
                Log.Warn("Requested service found on Consul with different case", unencryptedTags: new
                {
                    requestedService = serviceState.ServiceNameOrigin,
                    serviceOnConsul = serviceNameMatchByCase
                });
                serviceState.ServiceName = serviceNameMatchByCase;
            }

            return true;
        }

        private async Task LoadNodesByHealth(ConsulServiceState serviceState)
        {
            if (!serviceState.IsDeployed)
                return;

            await WaitIfErrorOccuredOnPreviousCall(serviceState).ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/health/service/{serviceState.ServiceName}?dc={DataCenter}&passing&index={serviceState.HealthModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, serviceState.ShutdownToken).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                serviceState.HealthModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                var nodes = TryDeserialize<ServiceEntry[]>(response.ResponseContent);
                if (nodes != null)
                {
                    if (
                        // Service has no nodes, but it did did have nodes before, and it is deployed
                        (nodes.Length == 0 && serviceState.Nodes.Length != 0 && serviceState.IsDeployed)
                        // Service has nodes, but it is not deployed
                        || (nodes.Length > 0 && !serviceState.IsDeployed))
                    {
                        // Try to reload version, to check if service deployment has changed
                        await ReloadServiceVersion(serviceState).ConfigureAwait(false);
                        if (serviceState.IsDeployed)
                        {
                            await LoadNodesByHealth(serviceState).ConfigureAwait(false);
                            return;
                        }
                    }
                    SetConsulNodes(nodes, serviceState, response, filterByVersion: true);
                    return;
                }
            }            
            SetErrorResult(serviceState, response, "Cannot extract service's nodes from Consul response");            
        }

        public async Task LoadNodesByQuery(ConsulServiceState serviceState)
        {
            var consulQuery = $"v1/query/{serviceState.ServiceName}/execute?dc={DataCenter}";
            var response = await CallConsul(consulQuery, serviceState.ShutdownToken).ConfigureAwait(false);

            if (response.IsDeployed == false)
            {
                SetServiceMissingResult(serviceState, response);
                return;
            }
            else if (response.Success)
            {
                var deserializedResponse = TryDeserialize<ConsulQueryExecuteResponse>(response.ResponseContent);
                if (deserializedResponse != null)
                {
                    SetConsulNodes(deserializedResponse.Nodes, serviceState, response, filterByVersion: false);
                    return;
                }
            }

            SetErrorResult(serviceState, response, "Cannot extract service's nodes from Consul query response");
        }

        private async Task WaitIfErrorOccuredOnPreviousCall(ConsulServiceState serviceState)
        {
            if (serviceState.LastResult?.Error != null)
            {
                var config = GetConfig();
                var now = DateTime.UtcNow;
                var timeElapsed = serviceState.LastResult.ResponseDateTime - now;
                if (timeElapsed < config.ErrorRetryInterval)
                    await DateTime.Delay(config.ErrorRetryInterval - timeElapsed).ConfigureAwait(false);
            }            
        }

        private async Task<ConsulResult> CallConsul(string urlCommand, CancellationToken cancellationToken)
        {
            var timeout = GetConfig().HttpTimeout;
            ulong? modifyIndex = 0;
            string requestLog = string.Empty;
            string responseContent = null;
            HttpStatusCode? statusCode = null;

            try
            {
                if (_httpClient == null)
                    _httpClient = new HttpClient { BaseAddress = ConsulAddress };

                requestLog = _httpClient.BaseAddress + urlCommand;
                using (var timeoutcancellationToken = new CancellationTokenSource(timeout))
                using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutcancellationToken.Token))
                using (var response = await _httpClient.GetAsync(urlCommand, HttpCompletionOption.ResponseContentRead, cancellationSource.Token).ConfigureAwait(false))
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    statusCode = response.StatusCode;
                    Log.Debug(x => x("Response received from Consul", unencryptedTags: new { ConsulAddress, requestLog = urlCommand, responseCode = statusCode, responseContent}));

                    var consulResponse = new ConsulResult {RequestLog = requestLog, StatusCode = statusCode, ResponseContent = responseContent, ResponseDateTime = DateTime.UtcNow};

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
                return new ConsulResult {RequestLog = requestLog, ResponseContent = responseContent, Error = ex, StatusCode = statusCode};
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

        internal void SetErrorResult(ConsulServiceState serviceState, ConsulResult result, string errorMessage)
        {
            if (result.Error==null)
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

        internal void SetServiceMissingResult(ConsulServiceState serviceState, ConsulResult consulResult)
        {
            lock (serviceState)
            {
                serviceState.IsDeployed = false;
                serviceState.Nodes = new INode[0];
                serviceState.LastResult = consulResult;
            }
        }

        private void SetConsulNodes(ServiceEntry[] consulNodes, ConsulServiceState serviceState, ConsulResult consulResult, bool filterByVersion)
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


        private int _disposed=0;
        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            _httpClient?.Dispose();
            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }
    }
}

