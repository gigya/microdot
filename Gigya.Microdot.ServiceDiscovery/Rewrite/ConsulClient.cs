﻿#region Copyright 
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
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    internal class ConsulClient : IConsulClient
    {
        private ILog Log { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private Uri ConsulAddress => _httpClient.BaseAddress;        
        private string Zone { get; }
        private HttpClient _httpClient;
        private int _disposed = 0;



        public ConsulClient(ILog log, IEnvironment environment, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            Zone = environment.Zone;
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;

            if (environment.ConsulAddress != null)
                _httpClient = new HttpClient { BaseAddress = new Uri($"http://{environment.ConsulAddress}") };
            else
                _httpClient = new HttpClient { BaseAddress = new Uri($"http://{CurrentApplicationInfo.HostName}:8500") };
        }


        internal async Task<ConsulResponse<ConsulNode[]>> GetHealthyNodes(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            string urlCommand = $"v1/health/service/{deploymentIdentifier.GetConsulServiceName()}?dc={deploymentIdentifier.Zone}&passing&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<ConsulNode[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var serviceEntries = JsonConvert.DeserializeObject<ServiceEntry[]>(response.ResponseContent);
                    response.Result = serviceEntries.Select(ToNode).ToArray();
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.Error == null)
                response.ConsulResponseError();

            return response;
        }


        public async Task<ConsulResponse<T>> GetKey<T>(ulong modifyIndex, string folder, string key,
            string zone = null, CancellationToken cancellationToken=default(CancellationToken)) where T: class
        {
            T result = null;
            string urlCommand = $"v1/kv/{folder}/{key}?dc={zone ?? Zone}&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<KeyValueResponse[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var keyValues = JsonConvert.DeserializeObject<KeyValueResponse[]>(response.ResponseContent);
                    result = keyValues.SingleOrDefault()?.DecodeValue<T>();                    
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
                response.IsUndeployed = true;
            else if (response.Error == null)
                response.ConsulResponseError();

            return response.SetResult(result);
        }

        internal async Task<ConsulResponse<string>> GetDeploymentVersion(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            string version = null;
            var response = await GetKey<ServiceKeyValue>(modifyIndex, "service", deploymentIdentifier.GetConsulServiceName(), deploymentIdentifier.Zone, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.IsUndeployed = true;
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                version = response.Result?.Version;
                response.IsUndeployed = false;
            }
            else if (response.Error == null)
                response.ConsulResponseError();
            return response.SetResult(version);
        }



        public async Task<ConsulResponse<string[]>> GetAllKeys(ulong modifyIndex, string folder, CancellationToken cancellationToken=default(CancellationToken))
        {
            string urlCommand = $"v1/kv/{folder}?dc={Zone}&keys&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var response = await Call<string[]>(urlCommand, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    var fullKeyNames = JsonConvert.DeserializeObject<string[]>(response.ResponseContent);
                    var keyNames = fullKeyNames.Select(s => s.Substring($"{folder}/".Length)).ToArray();
                    response.Result = keyNames;
                }
                catch (Exception ex)
                {
                    response.UnparsableConsulResponse(ex);
                }
            }
            else if (response.Error == null)
                response.ConsulResponseError();

            return response;
        }


        internal Task<ConsulResponse<string[]>> GetAllServices(ulong modifyIndex, CancellationToken cancellationToken)
        {
            return GetAllKeys(modifyIndex, "service", cancellationToken);
        }



        private async Task<ConsulResponse<T>> Call<T>(string commandPath, CancellationToken cancellationToken)
        {
            if (_disposed > 0)
                throw new ObjectDisposedException(nameof(ConsulClient));

            var timeout = GetConfig().HttpTaskTimeout;

            if (_httpClient.Timeout != timeout)
                _httpClient = new HttpClient { BaseAddress = ConsulAddress, Timeout = timeout };

            string responseContent = null;
            var consulResult = new ConsulResponse<T> { ConsulAddress = ConsulAddress.ToString(), CommandPath = commandPath };

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(commandPath, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

                using (response)
                {
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    consulResult.StatusCode = response.StatusCode;
                    consulResult.ResponseContent = responseContent;
                    consulResult.ResponseDateTime = DateTime.UtcNow;
                    consulResult.ModifyIndex = TryGetConsulIndex(response);
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

            return consulResult;
        }

        private ConsulNode ToNode(ServiceEntry serviceEntry)
        {
            const string versionPrefix = "version:";
            string versionTag = serviceEntry.Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            string version = versionTag?.Substring(versionPrefix.Length);

            return new ConsulNode(serviceEntry.Node.Name, serviceEntry.Service?.Port, version);
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
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            _httpClient.Dispose();
        }
    }
}