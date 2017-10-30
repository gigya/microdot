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
using System.Collections.Concurrent;
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
    public class ConsulClient : IConsulClient, IDisposable
    {
        private readonly string _serviceName;
        private readonly IDateTime _dateTime;
        private ILog Log { get; }
        private HttpClient _httpClient;

        private readonly JsonSerializerSettings _jsonSettings =
            new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

        private readonly AggregatingHealthStatus _aggregatedHealthStatus;

        private Func<ConsulDiscoveryConfig> GetConfig { get; }

        public Uri ConsulAddress { get; }

        private string DataCenter { get; }

        private CancellationTokenSource ShutdownToken { get; }

        private readonly BroadcastBlock<EndPointsResult> _resultChanged;

        private readonly TaskCompletionSource<bool> _initializedVersion;
        private TaskCompletionSource<bool> _waitForConfigChange;

        private ulong _endpointsModifyIndex = 0;
        private ulong _versionModifyIndex = 0;
        private string _activeVersion;
        private bool _isDeploymentDefined = true;

        private bool _disposed;

        public ConsulClient(string serviceName, Func<ConsulDiscoveryConfig> getConfig,
            ISourceBlock<ConsulDiscoveryConfig> configChanged, IEnvironmentVariableProvider environmentVariableProvider,
            ILog log, IDateTime dateTime, Func<string, AggregatingHealthStatus> getAggregatedHealthStatus)
        {
            _serviceName = serviceName;
            GetConfig = getConfig;
            _dateTime = dateTime;
            Log = log;
            DataCenter = environmentVariableProvider.DataCenter;

            _waitForConfigChange = new TaskCompletionSource<bool>();
            configChanged.LinkTo(new ActionBlock<ConsulDiscoveryConfig>(ConfigChanged));

            var address = environmentVariableProvider.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500";
            ConsulAddress = new Uri($"http://{address}");
            _httpClient = new HttpClient {BaseAddress = ConsulAddress, Timeout = TimeSpan.FromSeconds(5)};
            _aggregatedHealthStatus = getAggregatedHealthStatus("ConsulClient");

            _resultChanged = new BroadcastBlock<EndPointsResult>(null);
            _initializedVersion = new TaskCompletionSource<bool>();
            ShutdownToken = new CancellationTokenSource();
            Task.Run(RefreshVersionForever);
            Task.Run(RefreshEndpointsForever);
        }

        private async Task RefreshVersionForever()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                string version = null;
                var config = GetConfig();

                if (config.UseLongPolling)
                    version = await GetServiceVersion().ConfigureAwait(false);
                else
                {
                    await _waitForConfigChange.Task.ConfigureAwait(false);
                    continue;
                }

                var delay = TimeSpan.FromMilliseconds(100);
                if (version != null)
                {
                    _activeVersion = version;
                    _isDeploymentDefined = true;
                    _initializedVersion.TrySetResult(true);
                }
                else if (_isDeploymentDefined == false)
                    delay = config.UndefinedRetryInterval;
                else
                    delay = config.ErrorRetryInterval;

                await _dateTime.Delay(delay).ConfigureAwait(false);
            }
        }

        private async Task RefreshEndpointsForever()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                bool success = false;
                var config = GetConfig();
                try
                {
                    if (config.UseLongPolling)
                    {
                        await _initializedVersion.Task;
                        success = await GetEndpointsByHealth().ConfigureAwait(false);
                    }
                    else
                        success = await GetEndpointsByQuery().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log.Critical("Failed to load endpoints from Consul", e);
                }


                var delay = TimeSpan.FromMilliseconds(100);
                if (_isDeploymentDefined == false)
                    delay = config.UndefinedRetryInterval;
                else if (!success)
                    delay = config.ErrorRetryInterval;
                else if (config.UseLongPolling == false)
                    delay = config.ReloadInterval;

                await _dateTime.Delay(delay).ConfigureAwait(false);
            }
        }

        private EndPointsResult _result;

        public EndPointsResult Result
        {
            get => _result;
            set
            {
                _result = value;
                _resultChanged?.Post(_result);
            }
        }

        private async Task ConfigChanged(ConsulDiscoveryConfig c)
        {
            _waitForConfigChange.TrySetResult(true);
            _waitForConfigChange = new TaskCompletionSource<bool>();
        }

        private async Task<string> GetServiceVersion()
        {
            var config = GetConfig();
            var urlCommand = $"v1/kv/service/{_serviceName}?dc={DataCenter}&index={_versionModifyIndex}&wait={config.ReloadTimeout.Seconds}s";
            var response = await CallConsul(urlCommand, config.ReloadTimeout, i => _versionModifyIndex = i).ConfigureAwait(false);
            var keyValue = TryDeserialize<KeyValueResponse[]>(response);
            var version = keyValue?.FirstOrDefault()?.DecodeValue()?.Version;

            if (version == null && Result.IsQueryDefined)
                SetErrorResult(urlCommand, new EnvironmentException("Cannot extract service's active version from Consul response"), null, response);

            return version;
        }

        private async Task<bool> GetEndpointsByHealth()
        {
            if (!_isDeploymentDefined)
                return false;

            var config = GetConfig();            
            var urlCommand = $"v1/health/service/{_serviceName}?dc={DataCenter}&passing&index={_endpointsModifyIndex}&wait={config.ReloadTimeout.Seconds}s";
            var response = await CallConsul(urlCommand, config.ReloadTimeout, i => _endpointsModifyIndex = i).ConfigureAwait(false);
            var deserializedResponse = TryDeserialize<ServiceEntry[]>(response);
            if (deserializedResponse != null)
            {
                SetResult(deserializedResponse, urlCommand, response, _activeVersion);
                return true;
            }
            return false;
        }

        public async Task<bool> GetEndpointsByQuery()
        {
            var config = GetConfig();
            var consulQuery = $"v1/query/{_serviceName}/execute?dc={DataCenter}";
            var response = await CallConsul(consulQuery, config.ReloadTimeout, null);
            var deserializedResponse = TryDeserialize<ConsulQueryExecuteResponse>(response);
            if (deserializedResponse != null)
            {                
                SetResult(deserializedResponse.Nodes, consulQuery, response);
                return true;
            }
            return false;
        }

        private async Task<string> CallConsul(string urlCommand, TimeSpan timeout, Action<ulong> setModifyIndex)
        {
            var requestLog = _httpClient.BaseAddress + urlCommand;
            string responseContent = null;
            HttpStatusCode? statusCode = null;

            try
            {
                if (timeout != _httpClient.Timeout)
                    _httpClient = new HttpClient {BaseAddress = ConsulAddress, Timeout = timeout};

                using (var response = await _httpClient.GetAsync(urlCommand).ConfigureAwait(false))
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    statusCode = response.StatusCode;

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

                        if (statusCode == HttpStatusCode.NotFound ||
                            responseContent.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase))
                        {
                            SetUndefinedResult(requestLog, string.IsNullOrEmpty(responseContent) ? "404 NotFound" : responseContent);
                            return null;
                        }

                        Log.Error(_ => _("Error calling Consul", exception: exception));
                        SetErrorResult(requestLog, exception, statusCode, responseContent);
                        return null;
                    }

                    response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders);
                    if (consulIndexHeaders != null &&
                        ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out var consulIndexValue))
                        setModifyIndex?.Invoke(consulIndexValue);
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                Log.Error("Error calling Consul", exception: ex, unencryptedTags: new
                {
                    ServiceName = _serviceName,
                    ConsulAddress = ConsulAddress.ToString(),
                    consulQuery = urlCommand,
                    ResponseCode = statusCode,
                    Content = responseContent
                });
                
                SetErrorResult(requestLog, ex, statusCode, responseContent);
                return null;
            }
        }

        protected T TryDeserialize<T>(string response)
        {
            if (response == null)
                return default(T);
            try
            {
                return (T) JsonConvert.DeserializeObject(response, typeof(T), _jsonSettings);
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

            _aggregatedHealthStatus.RegisterCheck(_serviceName, ()=>HealthCheckResult.Unhealthy("Consul error: " + ex.Message));

            if (Result != null && Result.Error == null)
                return;

            Result = new EndPointsResult
            {
                EndPoints = new ConsulEndPoint[0],
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = ex.Message,
                Error = ex,
                IsQueryDefined = true
            };
        }

        internal void SetUndefinedResult(string requestLog, string responseContent)
        {
            _isDeploymentDefined = false;
            Result = new EndPointsResult
            {
                EndPoints = new ConsulEndPoint[0],
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = responseContent,
                IsQueryDefined = false
            };

            _aggregatedHealthStatus.RegisterCheck(_serviceName, () => HealthCheckResult.Healthy("Service not exists on Consul"));
        }

        private void SetResult(ServiceEntry[] nodes, string requestLog, string responseContent, string activeVersion = null)
        {
            if (!_isDeploymentDefined)
                return;

            var endpoints = nodes.Select(ep => new ConsulEndPoint
            {
                HostName = ep.Node.Name,
                ModifyIndex = ep.Node.ModifyIndex,
                Port = ep.Service.Port,
                Version = GetEndpointVersion(ep)
            });
            if (activeVersion != null)
                endpoints = endpoints.Where(ep => ep.Version == activeVersion);

            _aggregatedHealthStatus.RegisterCheck(_serviceName, () => HealthCheckResult.Healthy($"{endpoints.Count()} endpoints"));

            Result = new EndPointsResult
            {
                EndPoints = endpoints.ToArray(),
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = responseContent,
                IsQueryDefined = true,
                ActiveVersion = activeVersion ?? GetEndpointVersion(nodes.FirstOrDefault())
            };
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

            ShutdownToken.Cancel();
            _waitForConfigChange.TrySetResult(false);
            _initializedVersion.TrySetResult(false);            
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

        public ServiceKeyValue DecodeValue()
        {
            if (Value == null)
                return null;
            var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(Value));
            return (ServiceKeyValue) JsonConvert.DeserializeObject(serialized, typeof(ServiceKeyValue));
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