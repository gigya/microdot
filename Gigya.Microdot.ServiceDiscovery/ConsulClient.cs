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

        public async Task<EndPointsResult> GetEndPoints(string serviceName)
        {
            var consulQuery = $"/v1/query/{serviceName}/execute?dc={DataCenter}";
            var requestLog = _httpClient.BaseAddress + consulQuery;
            string responseContent = null;
            HttpStatusCode? statusCode = null;

            try
            {
                using (var response = await _httpClient.GetAsync(consulQuery).ConfigureAwait(false))
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    statusCode = response.StatusCode;

                    if (statusCode == HttpStatusCode.OK)
                    {
                        QueryIsOk(consulQuery, serviceName, queryExists: true);
                    }
                    else
                    {
                        var exception = new EnvironmentException("Consul response not OK",
                            unencrypted: new Tags
                            {
                                { "ConsulAddress", ConsulAddress.ToString() },
                                    {"ServiceDeployment", serviceName},
                                    {"ConsulQuery", consulQuery},
                                { "ResponseCode", statusCode.ToString() },
                                { "Content", responseContent }
                            });
                        
                        if (responseContent.EndsWith("Query not found", StringComparison.InvariantCultureIgnoreCase))
                        {
                            QueryIsOk(consulQuery, serviceName, queryExists: false);                            
                            return ErrorResult(requestLog, ex: null , isQueryDefined: false);
                        }

                        Log.Error(_ => _("Error calling Consul", exception: exception));
                        _failedQueries.TryAdd(consulQuery, exception);
                        return ErrorResult(requestLog, exception, true);
                    }
                }
                var serializedResponse = (ConsulQueryExecuteResponse)JsonConvert.DeserializeObject(responseContent, typeof(ConsulQueryExecuteResponse), _jsonSettings);
                var endpoints = serializedResponse.Nodes.Select(_ => new ConsulEndPoint { HostName = _.Node.Name, ModifyIndex = _.Node.ModifyIndex, Port = _.Service.Port }).ToArray();
                return SuccessResult(endpoints, requestLog, responseContent);
            }
            catch (Exception ex)
            {
                Log.Error("Error calling Consul", exception: ex, unencryptedTags: new
                {
                    ServiceName = serviceName,
                    ConsulAddress = ConsulAddress.ToString(),
                    consulQuery,
                    ResponseCode = statusCode,
                    Content = responseContent
                });

                _failedQueries.TryAdd(consulQuery, ex);
                return ErrorResult(requestLog, ex, true);
            }
        }

        private void QueryIsOk(string consulQuery, string serviceName, bool queryExists)
        {
            Exception e;
            _failedQueries.TryRemove(consulQuery, out e);
            _existingServices.AddOrUpdate(serviceName, queryExists, (_,__)=>queryExists);
        }

        internal static EndPointsResult SuccessResult(ConsulEndPoint[] endpoints, string requestLog, string responseContent)
        {
            return new EndPointsResult
            {
                EndPoints = endpoints,
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = responseContent,
                IsQueryDefined = true
            };
        }

        internal static EndPointsResult ErrorResult(string requestLog, Exception ex, bool isQueryDefined)
        {
            return new EndPointsResult
            {
                EndPoints = new ConsulEndPoint[0],
                RequestDateTime = DateTime.Now,
                RequestLog = requestLog,
                ResponseLog = ex?.ToString(),
                Error = ex,
                IsQueryDefined = isQueryDefined
            };
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
}