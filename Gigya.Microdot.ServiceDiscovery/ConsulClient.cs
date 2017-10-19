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
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulClient : IConsulClient
    {
        private ILog Log { get; }
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto, NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented };
        private readonly ConcurrentDictionary<string, Exception> _failedQueries = new ConcurrentDictionary<string, Exception>();
        private readonly ConcurrentDictionary<string, bool> _existingServices = new ConcurrentDictionary<string, bool>();

        public Uri ConsulAddress { get; }

        private string DataCenter { get; }

        public ConsulClient(IEnvironmentVariableProvider environmentVariableProvider, ILog log, HealthMonitor healthMonitor)
        {
            Log = log;
            DataCenter = environmentVariableProvider.DataCenter;
            var address = environmentVariableProvider.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500";
            ConsulAddress = new Uri($"http://{address}");
            _httpClient = new HttpClient { BaseAddress = ConsulAddress, Timeout = TimeSpan.FromSeconds(5) };
            healthMonitor.SetHealthFunction("ConsulClient", HealthCheck);
        }

        private HealthCheckResult HealthCheck()
        {
            var failedQueries = _failedQueries.ToArray();
            var nonExistQueries = _existingServices.Where(_ => _.Value == false).Select(_ => _.Key).ToArray();
            var nonExistQueriesStr = nonExistQueries.Any() ? $"The following queries do not exist: {string.Join(", ", nonExistQueries)}" : string.Empty;

            if (failedQueries.Any() == false)
                return HealthCheckResult.Healthy(nonExistQueriesStr);
            else
            {
                var failedQueriesStr = "The following queries failed: " + string.Join(", ", failedQueries.Select(q => $"\"{q.Key}\": {HealthMonitor.GetMessages(q.Value)}"));
                return HealthCheckResult.Unhealthy(nonExistQueriesStr + "\r\n" + failedQueriesStr);
            }
        }

        public Task<EndPointsResult> GetHealthyEndpoints(string serviceName, ulong index, TimeSpan timeout)
        {            
            var urlCommand = $"/v1/health/service/{serviceName}/?dc={DataCenter}&passing&index={index}&wait={timeout.Seconds}s";
            return GetConsulResult<ServiceEntry[]>(urlCommand, serviceName, SetResultEndpoints, timeout);
        }

        public Task<EndPointsResult> GetServiceVersion(string serviceName, ulong index, TimeSpan timeout)
        {
            var urlCommand = $"/v1/kv/service/{serviceName}/?dc={DataCenter}&index={index}&wait={timeout.Seconds}s";
            return GetConsulResult<KeyValueResponse[]>(urlCommand, serviceName, (v,r) => r.ActiveVersion = v.FirstOrDefault()?.DecodeValue().Version, timeout);
        }

        public async Task<EndPointsResult> GetQueryEndpoints(string serviceName)
        {            
            var consulQuery = $"/v1/query/{serviceName}/execute?dc={DataCenter}";
            var result = await GetConsulResult<ConsulQueryExecuteResponse>(consulQuery, serviceName, (n,r) => SetResultEndpoints(n.Nodes, r));

            if (result.Error != null)
                _failedQueries.TryAdd(consulQuery, result.Error);
            else
            {
                _failedQueries.TryRemove(consulQuery, out Exception _);
                _existingServices.AddOrUpdate(serviceName, result.IsQueryDefined, (_, __) => result.IsQueryDefined);
            }

            return result;
        }

        private async Task<EndPointsResult> GetConsulResult<TResponse>(string urlCommand, string serviceName, Action<TResponse, EndPointsResult> setResultByConsulResponse, TimeSpan? minTimeout=null)
        {
            var requestLog = _httpClient.BaseAddress + urlCommand;
            string responseContent = null;
            HttpStatusCode? statusCode = null;
            ulong? modifyIndex = null;

            try
            {
                if (minTimeout.HasValue && minTimeout.Value>_httpClient.Timeout)
                    _httpClient.Timeout = minTimeout.Value + TimeSpan.FromSeconds(1);

                using (var response = await _httpClient.GetAsync(urlCommand).ConfigureAwait(false))
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    statusCode = response.StatusCode;

                    if (statusCode != HttpStatusCode.OK)
                    {
                        var exception = new EnvironmentException("Consul response not OK",
                            unencrypted: new Tags
                            {
                                { "ConsulAddress", ConsulAddress.ToString() },
                                {"ServiceDeployment", serviceName},
                                {"ConsulQuery", urlCommand},
                                { "ResponseCode", statusCode.ToString() },
                                { "Content", responseContent }
                            });

                        if (statusCode==HttpStatusCode.NotFound || responseContent.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return ErrorResult(requestLog, ex: null, isQueryDefined: false);
                        }

                        Log.Error(_ => _("Error calling Consul", exception: exception));
                        _failedQueries.TryAdd(urlCommand, exception);
                        return ErrorResult(requestLog, exception, true);
                    }

                    response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders);
                    if (ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out var consulIndexValue))
                        modifyIndex = consulIndexValue;
                }
                var serializedResponse = (TResponse)JsonConvert.DeserializeObject(responseContent, typeof(TResponse), _jsonSettings);                                
                var result = SuccessResult(modifyIndex, requestLog, responseContent);
                setResultByConsulResponse(serializedResponse, result);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error("Error calling Consul", exception: ex, unencryptedTags: new
                {
                    ServiceName = serviceName,
                    ConsulAddress = ConsulAddress.ToString(),
                    consulQuery = urlCommand,
                    ResponseCode = statusCode,
                    Content = responseContent
                });

                _failedQueries.TryAdd(urlCommand, ex);
                return ErrorResult(requestLog, ex, true);
            }
        }

        internal static EndPointsResult SuccessResult(ulong? modifyIndex, string requestLog, string responseContent)
        {
            return new EndPointsResult
            {
                EndPoints = new ConsulEndPoint[0],
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = responseContent,
                IsQueryDefined = true,
                ModifyIndex = modifyIndex
            };
        }

        internal static EndPointsResult ErrorResult(string requestLog, Exception ex, bool isQueryDefined)
        {
            return new EndPointsResult
            {
                EndPoints = new ConsulEndPoint[0],
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = ex?.Message,
                Error = ex,
                IsQueryDefined = isQueryDefined
            };
        }

        public static void SetResultEndpoints(ServiceEntry[] nodes, EndPointsResult result)
        {
            result.EndPoints = nodes.Select(_ => new ConsulEndPoint { HostName = _.Node.Name, ModifyIndex = _.Node.ModifyIndex, Port = _.Service.Port }).ToArray();
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
            var serialized = Convert.ToBase64String(Encoding.UTF8.GetBytes(Value));
            return (ServiceKeyValue)JsonConvert.DeserializeObject(serialized, typeof(ServiceKeyValue));

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