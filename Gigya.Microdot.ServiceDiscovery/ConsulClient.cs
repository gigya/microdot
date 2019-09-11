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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Newtonsoft.Json;
#pragma warning disable 1591

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulClient : IConsulClient
    {
        private string _serviceName;
        private readonly string _serviceNameOrigin;

        private readonly IDateTime _dateTime;
        private ILog Log { get; }
        private readonly HttpClient _httpClient;

        private readonly object _setResultLocker = new object();

        private Func<ConsulConfig> GetConfig { get; }

        public Uri ConsulAddress { get; }

        private string Zone { get; }

        private CancellationTokenSource ShutdownToken { get; }

        private readonly BufferBlock<EndPointsResult> _resultChanged;

        private readonly TaskCompletionSource<bool> _initializedVersion;
        private TaskCompletionSource<bool> _waitForConfigChange;

        private ulong _endpointsModifyIndex;
        private ulong _versionModifyIndex;
        private ulong _allKeysModifyIndex;
        private string _activeVersion;
        private bool _isDeploymentDefined = true;

        private bool _disposed;
        private int _initialized;
        private readonly IDisposable _healthCheck;
        private Func<HealthCheckResult> _getHealthStatus;

        public ConsulClient(string serviceName, Func<ConsulConfig> getConfig,
            ISourceBlock<ConsulConfig> configChanged, IEnvironment environment,
            ILog log, IDateTime dateTime, Func<string, AggregatingHealthStatus> getAggregatedHealthStatus, 
            CurrentApplicationInfo appInfo)
        {
            _serviceName = serviceName;
            _serviceNameOrigin = serviceName;

            GetConfig = getConfig;
            _dateTime = dateTime;
            Log = log;
            Zone = environment.Zone;

            _waitForConfigChange = new TaskCompletionSource<bool>();
            configChanged.LinkTo(new ActionBlock<ConsulConfig>(ConfigChanged));

            ConsulAddress = new Uri($"http://{environment.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500"}");
            _httpClient = new HttpClient { BaseAddress = ConsulAddress, Timeout = TimeSpan.FromMinutes(100) }; // timeout will be implemented using cancellationToken when calling httpClient
            var aggregatedHealthStatus = getAggregatedHealthStatus("ConsulClient");

            _resultChanged = new BufferBlock<EndPointsResult>();
            _initializedVersion = new TaskCompletionSource<bool>();
            ShutdownToken = new CancellationTokenSource();
            _healthCheck = aggregatedHealthStatus.RegisterCheck(_serviceNameOrigin, () => _getHealthStatus());
        }

        public Task Init()
        {
            if (Interlocked.Increment(ref _initialized) != 1)
                return Task.FromResult(1);

#pragma warning disable 4014
            // Run these loops in background
            LoadVersionLoop();
            LoadEndpointsLoop();
#pragma warning restore 4014
            return Task.FromResult(1);
        }

        private async Task LoadVersionLoop()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                var config = GetConfig();

                if (config.LongPolling)
                {
                    var response = await LoadServiceVersion().ConfigureAwait(false);
                    var delay = TimeSpan.FromMilliseconds(0);

                    if (response.Success)
                        _initializedVersion.TrySetResult(true);                
                    else if (response.Error != null)
                        delay = config.ErrorRetryInterval;

                    await _dateTime.Delay(delay).ConfigureAwait(false);
                }
                else
                {
                    await _waitForConfigChange.Task.ConfigureAwait(false);
                }
            }
        }

        private async Task LoadEndpointsLoop()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                ConsulResponse consulResponse;
                var config = GetConfig();

                var delay = TimeSpan.FromMilliseconds(0);
                if (config.LongPolling)
                {
                    await _initializedVersion.Task;
                    consulResponse = await LoadEndpointsByHealth().ConfigureAwait(false);
                }
                else
                {
                    consulResponse = await LoadEndpointsByQuery().ConfigureAwait(false);
                    delay = config.ReloadInterval;
                }

                if (consulResponse.Error != null)
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

        private Task ConfigChanged(ConsulConfig c)
        {
            _waitForConfigChange.TrySetResult(true);
            _waitForConfigChange = new TaskCompletionSource<bool>();
            return Task.FromResult(1);
        }

        private async Task<ConsulResponse> LoadServiceVersion()
        {
            var urlCommand = $"v1/kv/service/{_serviceName}?dc={Zone}&index={_versionModifyIndex}";
            var response = await CallConsul(urlCommand, ShutdownToken.Token, longPolling: true).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _versionModifyIndex = response.ModifyIndex.Value;

            if (response.IsDeploymentDefined == false)
                await SearchServiceInAllKeys().ConfigureAwait(false);
            else if (response.Success)
            {
                var keyValue = TryDeserialize<KeyValueResponse[]>(response.ResponseContent);
                try
                {
                    var version = keyValue?.SingleOrDefault()?.DecodeValue<ServiceKeyValue>()?.Version;
                    if (version==null)
                        throw new EnvironmentException("Consul key-value response not contains Version");

                    lock (_setResultLocker)
                    {
                        _activeVersion = version;
                        _isDeploymentDefined = true;
                        ForceReloadEndpointsByHealth();
                    }
                }
                catch(Exception ex)
                {
                    var exception = new EnvironmentException("Cannot extract service's active version from Consul response", innerException: ex);
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
            var urlCommand = $"v1/kv/service?dc={Zone}&keys&index={_allKeysModifyIndex}";
            var response = await CallConsul(urlCommand, ShutdownToken.Token, longPolling: true).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _allKeysModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                var services = TryDeserialize<string[]>(response.ResponseContent);
                var serviceNameMatchByCase = services.FirstOrDefault(s =>
                    s.Equals($"service/{_serviceName}", StringComparison.InvariantCultureIgnoreCase))?.Substring("service/".Length);

                var serviceExists = serviceNameMatchByCase != null;
                response.IsDeploymentDefined = serviceExists;

                if (!serviceExists)
                    SetServiceMissingResult(urlCommand, response.ResponseContent);

                var tryReloadServiceWithCaseMatching = serviceExists && _serviceName != serviceNameMatchByCase;
                if (tryReloadServiceWithCaseMatching)
                {
                    Log.Warn("Requested service found on Consul with different case", unencryptedTags: new
                    {
                        requestedService = _serviceNameOrigin,
                        serviceOnConsul = serviceNameMatchByCase,
                        consulResponse = response.ResponseContent
                    });
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
                return new ConsulResponse { IsDeploymentDefined = false };

            var urlCommand = $"v1/health/service/{_serviceName}?dc={Zone}&passing&index={_endpointsModifyIndex}";
            var response = await CallConsul(urlCommand, _loadEndpointsByHealthCancellationTokenSource.Token, longPolling: true).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _endpointsModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                var nodes = TryDeserialize<ServiceEntry[]>(response.ResponseContent);
                if (nodes != null)
                {
                    if (
                        // Service has no nodes, but it did did have nodes before, and it is not deployed
                        (nodes.Length == 0 && Result?.EndPoints?.Length != 0 && _isDeploymentDefined)
                        // Service has nodes, but it is not deployed
                        || (nodes.Length > 0 && !_isDeploymentDefined))
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
            var consulQuery = $"v1/query/{_serviceName}/execute?dc={Zone}";
            var response = await CallConsul(consulQuery, ShutdownToken.Token, longPolling: false).ConfigureAwait(false);

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

        private async Task<ConsulResponse> CallConsul(string urlCommand, CancellationToken cancellationToken, bool longPolling )
        {            
            var requestLog = string.Empty;
            var httpTaskTimeout = GetConfig().HttpTaskTimeout;
            var urlTimeout = GetConfig().HttpTimeout;
            string responseContent = null;
            HttpStatusCode? statusCode = null;

            if (longPolling)
                urlCommand += $"&wait={urlTimeout.TotalSeconds}s";

            try
            {
                requestLog = _httpClient.BaseAddress + urlCommand;

                ulong? modifyIndex;
                using (var timeoutcancellationToken = new CancellationTokenSource(httpTaskTimeout))
                using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutcancellationToken.Token))
                using (var response = await _httpClient.GetAsync(urlCommand, HttpCompletionOption.ResponseContentRead, cancellationSource.Token).ConfigureAwait(false))
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
                                {"ServiceName", _serviceName},
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

            _getHealthStatus = () => HealthCheckResult.Unhealthy($"Consul error: {ex.Message}");

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
            var stillNeedToCheckIfServiceExistOnAllKeysList = GetConfig().LongPolling && _allKeysModifyIndex == 0;
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

                _getHealthStatus = () => HealthCheckResult.Healthy("Service doesn't exist on Consul");
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
                }).OrderBy(x => x.HostName).ThenBy(x => x.Port).ToArray();

                ConsulEndPoint[] activeVersionEndpoints;
                string healthMessage;
                if (activeVersion == null)
                {
                    activeVersionEndpoints = endpoints;
                    healthMessage = $"{activeVersionEndpoints.Length} endpoints";
                }
                else
                {
                    activeVersionEndpoints = endpoints.Where(ep => ep.Version == activeVersion).ToArray();
                    healthMessage = $"{activeVersionEndpoints.Length} endpoints";
                    if (activeVersionEndpoints.Length != endpoints.Length)
                        healthMessage += $" matching to version {activeVersion} from total of {endpoints.Length} endpoints";
                }

                _getHealthStatus = () => HealthCheckResult.Healthy(_serviceName == _serviceNameOrigin
                    ? healthMessage
                    : $"Service exists on Consul, but with different casing: '{_serviceName}'. {healthMessage}");

                Result = new EndPointsResult
                {
                    EndPoints = activeVersionEndpoints,
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

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            ShutdownToken.Cancel();
            _loadEndpointsByHealthCancellationTokenSource?.Cancel();
            _waitForConfigChange.TrySetCanceled();
            _initializedVersion.TrySetCanceled();
            _healthCheck.Dispose();            
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

}