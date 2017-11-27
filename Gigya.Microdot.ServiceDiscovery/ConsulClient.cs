#region Copyright 

// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulClient : IConsulClient
    {
        private string _serviceName;
        private readonly IDateTime _dateTime;
        private ILog Log { get; }
        private HttpClient _httpClient;

        private readonly AggregatingHealthStatus _aggregatedHealthStatus;

        private object _setResultLocker = new object();

        private Func<ConsulConfig> GetConfig { get; }

        public Uri ConsulAddress { get; }

        private string DataCenter { get; }

        private CancellationTokenSource ShutdownToken { get; }

        private readonly BufferBlock<EndPointsResult> _resultChanged;

        private readonly TaskCompletionSource<bool> _initializedVersion;
        private TaskCompletionSource<bool> _waitForConfigChange;

        private ulong _endpointsModifyIndex = 0;
        private ulong _versionModifyIndex = 0;
        private ulong _allKeysModifyIndex = 0;
        private string _activeVersion;
        private bool _isDeploymentDefined = true;

        private bool _disposed;

        public ConsulClient(string serviceName, Func<ConsulConfig> getConfig,
            ISourceBlock<ConsulConfig> configChanged, IEnvironmentVariableProvider environmentVariableProvider,
            ILog log, IDateTime dateTime, Func<string, AggregatingHealthStatus> getAggregatedHealthStatus)
        {
            _serviceName = serviceName;
            GetConfig = getConfig;
            _dateTime = dateTime;
            Log = log;
            DataCenter = environmentVariableProvider.DataCenter;

            _waitForConfigChange = new TaskCompletionSource<bool>();
            configChanged.LinkTo(new ActionBlock<ConsulConfig>(ConfigChanged));

            var address = environmentVariableProvider.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500";
            ConsulAddress = new Uri($"http://{address}");
            _aggregatedHealthStatus = getAggregatedHealthStatus("ConsulClient");

            _resultChanged = new BufferBlock<EndPointsResult>();
            _initializedVersion = new TaskCompletionSource<bool>();
            ShutdownToken = new CancellationTokenSource();
            Task.Run(LoadVersionLoop);
            Task.Run(LoadEndpointsLoop);
        }

        private async Task LoadVersionLoop()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                ConsulResponse response = null;
                var config = GetConfig();

                if (config.UseLongPolling)
                    response = await LoadServiceVersion().ConfigureAwait(false);
                else
                {
                    await _waitForConfigChange.Task.ConfigureAwait(false);
                    continue;
                }

                var delay = TimeSpan.FromMilliseconds(0);

                if (response.Success)
                    _initializedVersion.TrySetResult(true);                
                else if (response.Error!=null)
                    delay = config.ErrorRetryInterval;

                await _dateTime.Delay(delay).ConfigureAwait(false);
            }
        }

        private async Task LoadEndpointsLoop()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                ConsulResponse consulResponse = null;
                var config = GetConfig();

                var delay = TimeSpan.FromMilliseconds(0);
                if (config.UseLongPolling)
                {
                    await _initializedVersion.Task;
                    consulResponse = await LoadEndpointsByHealth().ConfigureAwait(false);
                }
                else
                {
                    consulResponse = await LoadEndpointsByQuery().ConfigureAwait(false);
                    delay = config.ReloadInterval;
                }

                if (consulResponse.Error!=null)
                    delay = config.ErrorRetryInterval;

                await _dateTime.Delay(delay).ConfigureAwait(false);
            }
        }

        private EndPointsResult _result;
        private CancellationTokenSource _loadEndpointsByHealthCancellationTokenSource;

        public EndPointsResult Result
        {
            get => _result;
            set
            {
                lock (_setResultLocker)
                {
                    if (value?.Equals(Result) == true)
                        return;

                    _result = value;
                    _resultChanged.Post(_result);
                }
            }
        }

        private async Task ConfigChanged(ConsulConfig c)
        {
            _waitForConfigChange.TrySetResult(true);
            _waitForConfigChange = new TaskCompletionSource<bool>();
        }

        private async Task<ConsulResponse> LoadServiceVersion()
        {
            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/kv/service/{_serviceName}?dc={DataCenter}&index={_versionModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _versionModifyIndex = response.ModifyIndex.Value;

            if (response.IsDeploymentDefined==false)                
                    await SearchServiceInAllKeys().ConfigureAwait(false);
            else if (response.Success)
            {
                var keyValue = TryDeserialize<KeyValueResponse[]>(response.ResponseContent);
                var version = keyValue?.SingleOrDefault()?.TryDecodeValue()?.Version;

                if (version != null)
                {
                    lock (_setResultLocker)
                    {
                        _activeVersion = version;
                        _isDeploymentDefined = true;
                        ForceReloadEndpointsByHealth();
                    }
                }
                else
                {
                    var exception = new EnvironmentException("Cannot extract service's active version from Consul response");
                    SetErrorResult(urlCommand, exception, null, response.ResponseContent);
                    response.Error = exception;
                }
            }
            return response;
        }

        private Task<ConsulResponse> ReloadServiceVersion()
        {
            _versionModifyIndex = 0;
            return LoadServiceVersion();
        }

        private async Task<ConsulResponse> SearchServiceInAllKeys()
        {
            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/kv/service?dc={DataCenter}&keys&index={_allKeysModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _allKeysModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                var services = TryDeserialize<string[]>(response.ResponseContent);
                var serviceNameMatchByCase = services.FirstOrDefault(s =>
                    s.Equals(_serviceName, StringComparison.InvariantCultureIgnoreCase));

                var serviceExists = serviceNameMatchByCase != null;
                response.IsDeploymentDefined = serviceExists;

                if (!serviceExists)
                    SetServiceMissingResult(urlCommand, response.ResponseContent);

                var tryReloadServiceWithCaseMatching = serviceExists && _serviceName != serviceNameMatchByCase;
                if (tryReloadServiceWithCaseMatching)
                {
                    _serviceName = serviceNameMatchByCase;
                    _allKeysModifyIndex = 0;
                }
            }

            return response;
        }

        private async Task<ConsulResponse> LoadEndpointsByHealth()
        {
            _loadEndpointsByHealthCancellationTokenSource = new CancellationTokenSource();

            if (!_isDeploymentDefined)
                return new ConsulResponse {IsDeploymentDefined = false};

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/health/service/{_serviceName}?dc={DataCenter}&passing&index={_endpointsModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, _loadEndpointsByHealthCancellationTokenSource.Token).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _endpointsModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                var nodes = TryDeserialize<ServiceEntry[]>(response.ResponseContent);
                if (nodes != null)
                {
                    if  (
                        // Service has no nodes, but it did did have nodes before, and it is not deployed
                        (nodes.Length == 0 && Result?.EndPoints?.Length != 0 && _isDeploymentDefined) 
                        // Service has nodes, but it is not deployed
                        || (nodes.Length>0 && !_isDeploymentDefined))
                    {                        
                        // Try to reload version, to check if service deployment has changed
                        await ReloadServiceVersion();
                    }
                    SetResult(nodes, urlCommand, response.ResponseContent, _activeVersion);
                }
                else
                {
                    var exception = new EnvironmentException("Cannot extract service's nodes from Consul response");
                    SetErrorResult(urlCommand, exception, null, response.ResponseContent);
                    response.Error = exception;
                }
            }
            return response;
        }

        private void ForceReloadEndpointsByHealth()
        {
            _endpointsModifyIndex = 0;
            _loadEndpointsByHealthCancellationTokenSource?.Cancel();
        }

        private async Task<ConsulResponse> LoadEndpointsByQuery()
        {            
            var consulQuery = $"v1/query/{_serviceName}/execute?dc={DataCenter}";
            var response = await CallConsul(consulQuery, ShutdownToken.Token).ConfigureAwait(false);

            if (response.Success)
            {
                var deserializedResponse = TryDeserialize<ConsulQueryExecuteResponse>(response.ResponseContent);
                if (deserializedResponse != null)
                {
                    lock (_setResultLocker)
                    {
                        _isDeploymentDefined = true;
                        SetResult(deserializedResponse.Nodes, consulQuery, response.ResponseContent);
                    }
                }
                else
                {
                    var exception =
                        new EnvironmentException("Cannot extract service's nodes from Consul query response");
                    SetErrorResult(consulQuery, exception, null, response.ResponseContent);
                    response.Error = exception;
                }
            }
            return response;
        }

        private async Task<ConsulResponse> CallConsul(string urlCommand, CancellationToken cancellationToken)
        {
            var timeout = GetConfig().HttpTimeout;
            ulong? modifyIndex = 0;
            string requestLog = string.Empty;
            string responseContent = null;
            HttpStatusCode? statusCode = null;

            try
            {
                if (_httpClient==null || timeout != _httpClient.Timeout)
                    _httpClient = new HttpClient {BaseAddress = ConsulAddress, Timeout = timeout};

                requestLog = _httpClient.BaseAddress + urlCommand;

                using (var response = await _httpClient.GetAsync(urlCommand, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    statusCode = response.StatusCode;
                    Log.Debug(x => x("Response received from Consul", unencryptedTags: new { ConsulAddress, serviceDeployment = _serviceName, requestLog = urlCommand, responseCode = statusCode, responseContent, activeVersion = _activeVersion }));

                    if (statusCode != HttpStatusCode.OK)
                    {
                        var exception = new EnvironmentException("Consul response not OK",
                            unencrypted: new Tags
                            {
                                {"ConsulAddress", ConsulAddress.ToString()},
                                {"ServiceDeployment", _serviceName},
                                {"ConsulQuery", urlCommand},
                                {"ResponseCode", statusCode.ToString()},
                                {"Content", responseContent}
                            });

                        if (statusCode == HttpStatusCode.NotFound || responseContent.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var responseStr = string.IsNullOrEmpty(responseContent) ? "404 NotFound" : responseContent;
                            SetServiceMissingResult(requestLog, responseStr);
                            return new ConsulResponse { IsDeploymentDefined = false };
                        }

                        SetErrorResult(requestLog, exception, statusCode, responseContent);
                        return new ConsulResponse { Error = exception };
                    }

                    modifyIndex = GetConsulIndex(response);
                }

                return new ConsulResponse { ModifyIndex = modifyIndex, ResponseContent = responseContent };
            }
            catch (Exception ex)
            {
                if (!(ex is TaskCanceledException))
                    SetErrorResult(requestLog, ex, statusCode, responseContent);

                return new ConsulResponse { Error = ex };
            }
        }


        private static ulong? GetConsulIndex(HttpResponseMessage response)
        {
            ulong? modifyIndex=null;
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

        internal void SetErrorResult(string requestLog, Exception ex, HttpStatusCode? responseCode, string responseContent)
        {
            Log.Error("Error calling Consul", exception: ex, unencryptedTags: new
            {
                ServiceName = _serviceName,
                ConsulAddress = ConsulAddress.ToString(),
                consulQuery = requestLog,
                ResponseCode = responseCode,
                Content = responseContent
            });

            _aggregatedHealthStatus.RegisterCheck(_serviceName, ()=>HealthCheckResult.Unhealthy($"{_serviceName} - Consul error: " + ex.Message));

            if (Result != null && Result.Error == null)
                return;

            Result = new EndPointsResult
            {
                EndPoints = new ConsulEndPoint[0],
                RequestDateTime = DateTime.UtcNow,
                RequestLog = requestLog,
                ResponseLog = ex.Message,
                Error = ex,
                IsQueryDefined = true
            };
        }

        internal void SetServiceMissingResult(string requestLog, string responseContent)
        {
            var stillNeedToCheckIfServiceExistOnAllKeysList = GetConfig().UseLongPolling && _allKeysModifyIndex == 0;
            if (stillNeedToCheckIfServiceExistOnAllKeysList)
                return;

            lock (_setResultLocker)
            {
                _isDeploymentDefined = false;
                Result = new EndPointsResult
                {
                    EndPoints = new ConsulEndPoint[0],
                    RequestDateTime = DateTime.UtcNow,
                    RequestLog = requestLog,
                    ResponseLog = responseContent,
                    IsQueryDefined = false
                };

                _aggregatedHealthStatus.RegisterCheck(_serviceName,
                    () => HealthCheckResult.Healthy($"{_serviceName} - Service doesn't exist on Consul"));
            }
        }

        private void SetResult(ServiceEntry[] nodes, string requestLog, string responseContent, string activeVersion = null)
        {
            lock (_setResultLocker)
            {
                if (!_isDeploymentDefined)
                    return;

                var endpoints = nodes.Select(ep => new ConsulEndPoint
                {
                    HostName = ep.Node.Name,
                    Port = ep.Service.Port,
                    Version = GetEndpointVersion(ep)
                }).ToArray();

                ConsulEndPoint[] activeVersionEndpoints;
                string healthMessage = null;
                if (activeVersion == null)
                {
                    activeVersionEndpoints = endpoints.ToArray();
                    healthMessage = $"{activeVersionEndpoints.Length} endpoints";
                }
                else
                {
                    activeVersionEndpoints = endpoints.Where(ep => ep.Version == activeVersion).ToArray();
                    healthMessage = $"{activeVersionEndpoints.Length} endpoints";
                    if (activeVersionEndpoints.Length != endpoints.Length)
                        healthMessage +=
                            $" matching to version {activeVersion} from total of {endpoints.Length} endpoints";
                }


                _aggregatedHealthStatus.RegisterCheck(_serviceName,
                    () => HealthCheckResult.Healthy($"{_serviceName} - {healthMessage}"));

                Result = new EndPointsResult
                {
                    EndPoints = activeVersionEndpoints.ToArray(),
                    RequestDateTime = DateTime.UtcNow,
                    RequestLog = requestLog,
                    ResponseLog = responseContent,
                    IsQueryDefined = true,
                    ActiveVersion = activeVersion ?? GetEndpointVersion(nodes.FirstOrDefault())
                };
            }
        }

        private static string GetEndpointVersion(ServiceEntry node)
        {
            const string versionPrefix = "version:";
            var versionTag = node?.Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            return versionTag?.Substring(versionPrefix.Length);
        }

        public ISourceBlock<EndPointsResult> ResultChanged => _resultChanged;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;

            ShutdownToken.Cancel();
            _loadEndpointsByHealthCancellationTokenSource?.Cancel();
            _waitForConfigChange.TrySetResult(false);
            _initializedVersion.TrySetResult(false);            
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private class ConsulResponse
        {
            public bool IsDeploymentDefined { get; set; } = true;
            public Exception Error { get; set; }

            public bool Success => Error == null && IsDeploymentDefined;

            public string ResponseContent { get; set; }
            public ulong? ModifyIndex { get; set; }

        }
    }

    public class ConsulQueryExecuteResponse
    {
        public string Service { get; set; }

        public ServiceEntry[] Nodes { get; set; }

        public QueryDNSOptions DNS { get; set; }

        public string Datacenter { get; set; }

        public int Failovers { get; set; }
    }

    public class ServiceEntry
    {
        public Node Node { get; set; }

        public AgentService Service { get; set; }

        public HealthCheck[] Checks { get; set; }
    }

    public class Node
    {
        [JsonProperty(PropertyName = "Node")]
        public string Name { get; set; }

        public string Address { get; set; }

        public ulong ModifyIndex { get; set; }

        public Dictionary<string, string> TaggedAddresses { get; set; }
    }

    public class AgentService
    {
        public string ID { get; set; }

        public string Service { get; set; }

        public string[] Tags { get; set; }

        public int Port { get; set; }

        public string Address { get; set; }

        public bool EnableTagOverride { get; set; }
    }

    public class HealthCheck
    {
        public string Node { get; set; }

        public string CheckID { get; set; }

        public string Name { get; set; }

        public string Status { get; set; }

        public string Notes { get; set; }

        public string Output { get; set; }

        public string ServiceID { get; set; }

        public string ServiceName { get; set; }
    }

    public class QueryDNSOptions
    {
        public string TTL { get; set; }
    }

    public class KeyValueResponse
    {
        public int LockIndex { get; set; }
        public string Key { get; set; }
        public int Flags { get; set; }
        public string Value { get; set; }
        public ulong CreateIndex { get; set; }
        public ulong ModifyIndex { get; set; }

        public ServiceKeyValue TryDecodeValue()
        {
            if (Value == null)
                return null;

            try
            {
                var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(Value));
                return JsonConvert.DeserializeObject<ServiceKeyValue>(serialized);
            }
            catch
            {
                return null;
            }
        }
    }

    public class ServiceKeyValue
    {
        [JsonProperty("basePort")]
        public int BasePort { get; set; }

        [JsonProperty("dc")]
        public string DataCenter { get; set; }

        [JsonProperty("env")]
        public string Environment { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
}