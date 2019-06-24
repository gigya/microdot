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
using Gigya.Common.Contracts.Exceptions;
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
        private readonly HttpClient _httpClient;
        private bool _disposed;



        public ConsulClient(ILog log, IEnvironment environment, IDateTime dateTime, Func<ConsulConfig> getConfig, CurrentApplicationInfo appInfo)
        {
            Zone = environment.Zone;
            Log = log;
            DateTime = dateTime;
            GetConfig = getConfig;

            // timeout will be implemented using cancellationToken when calling httpClient
            // we assume a Consul agent is installed locally on the machine.
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"http://{environment.ConsulAddress ?? $"{CurrentApplicationInfo.HostName}:8500"}"),
                Timeout = TimeSpan.FromMinutes(100)
            };
        }

        /// <remarks>In case Consul doesn't have a change and the wait time passed, Consul will return a response to the query (with no changes since last call).</remarks>
        internal async Task<ConsulResponse<ConsulNode[]>> GetHealthyNodes(DeploymentIdentifier deploymentIdentifier, ulong modifyIndex, CancellationToken cancellationToken)
        {
            var urlCommand = $"v1/health/service/{deploymentIdentifier.GetConsulServiceName()}?dc={deploymentIdentifier.Zone}&passing&index={modifyIndex}";
            var response = await Call<ConsulNode[]>(urlCommand, cancellationToken, longPolling: true).ConfigureAwait(false);
	        if (response.StatusCode != HttpStatusCode.OK)
	        {
		        if (response.Error == null)
			        response.Error = response.ConsulResponseCodeNotOk();
	        }
			else
	        {
                try
                {
                    var serviceEntries = JsonConvert.DeserializeObject<ServiceEntry[]>(response.ResponseContent);
                    response.ResponseObject = serviceEntries.Select(ToNode).ToArray();
                }
                catch (Exception ex)
                {
                    response.Error = response.UnparsableConsulResponse(ex);
                }
            }

            return response;
        }


	    public Task<ConsulResponse<T>> GetKeyFromOtherZone<T>(ulong modifyIndex, string folder, string key, string zone, CancellationToken cancellationToken = default(CancellationToken)) where T : class
	    {
			if (zone==null)
				throw new ArgumentNullException(nameof(zone));

		    return GetKey<T>(modifyIndex, folder, key, zone, cancellationToken);
	    }

	    public Task<ConsulResponse<T>> GetKey<T>(ulong modifyIndex, string folder, string key, CancellationToken cancellationToken = default(CancellationToken)) where T : class
	    {
		    return GetKey<T>(modifyIndex, folder, key, Zone, cancellationToken);
	    }

        /// <remarks>In case Consul doesn't have a change and the wait time passed, Consul will return a response to the query (with no changes since last call).</remarks>
		private async Task<ConsulResponse<T>> GetKey<T>(ulong modifyIndex, string folder, string key, string zone, CancellationToken cancellationToken) where T: class
        {
			if (folder==null)
				throw new ArgumentNullException(nameof(folder));
	        if (key == null)
		        throw new ArgumentNullException(nameof(key));
	        if (zone == null)
		        throw new ArgumentNullException(nameof(zone));

			T result = null;
            var urlCommand = $"v1/kv/{folder}/{key}?dc={zone}&index={modifyIndex}";
            var response = await Call<KeyValueResponse[]>(urlCommand, cancellationToken, longPolling: true).ConfigureAwait(false);

	        if (response.StatusCode == HttpStatusCode.NotFound)
		        response.IsUndeployed = true;
			else if (response.StatusCode != HttpStatusCode.OK)
	        {
		        if (response.Error == null)
			        response.Error = response.ConsulResponseCodeNotOk();
			}
	        else
	        {
		        try
		        {
			        var keyValues = JsonConvert.DeserializeObject<KeyValueResponse[]>(response.ResponseContent);
			        result = keyValues.SingleOrDefault()?.DecodeValue<T>();
		        }
		        catch (Exception ex)
		        {
			        response.Error = response.UnparsableConsulResponse(ex);
		        }
	        }

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
			else if (response.StatusCode != HttpStatusCode.OK)
			{
	            if (response.Error == null)
		            response.Error = response.ConsulResponseCodeNotOk();
			}
            else
            {
                version = response.ResponseObject?.Version;
                response.IsUndeployed = false;
            }

            return response.SetResult(version);
        }



        /// <inheritdoc />
        /// <remarks>In case Consul doesn't have a change and the wait time passed, Consul will return a response to the query (with no changes since last call).</remarks>
        public async Task<ConsulResponse<string[]>> GetAllKeys(ulong modifyIndex, string folder, CancellationToken cancellationToken=default(CancellationToken))
        {
            var urlCommand = $"v1/kv/{folder}?dc={Zone}&keys&index={modifyIndex}";
            var response = await Call<string[]>(urlCommand, cancellationToken, longPolling: true).ConfigureAwait(false);
	        if (response.StatusCode != HttpStatusCode.OK)
	        {
		        if (response.Error == null)
			        response.Error = response.ConsulResponseCodeNotOk();
			}
			else
            {
                try
                {
                    var fullKeyNames = JsonConvert.DeserializeObject<string[]>(response.ResponseContent);
                    var keyNames = fullKeyNames.Select(s => s.Substring($"{folder}/".Length)).ToArray();
                    response.ResponseObject = keyNames;
                }
                catch (Exception ex)
                {
                    response.Error = response.UnparsableConsulResponse(ex);
                }
            }

            return response;
        }


        internal Task<ConsulResponse<string[]>> GetAllServices(ulong modifyIndex, CancellationToken cancellationToken)
        {
            return GetAllKeys(modifyIndex, "service", cancellationToken);
        }



        private async Task<ConsulResponse<T>> Call<T>(string commandPath, CancellationToken cancellationToken, bool longPolling)
        {
            if (_disposed)
                return new ConsulResponse<T>{Error = new EnvironmentException("ConsulClient already disposed")};

            var httpTaskTimeout = GetConfig().HttpTaskTimeout;
            var urlTimeout = GetConfig().HttpTimeout;

            if (longPolling)
                commandPath += $"&wait={urlTimeout.TotalSeconds}s";

            string responseContent;
            var consulResult = new ConsulResponse<T> { ConsulAddress = ConsulAddress.ToString(), CommandPath = commandPath };

            try
            {
	            using (var timeoutcancellationToken = new CancellationTokenSource(httpTaskTimeout))
	            using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutcancellationToken.Token))
	            {
		            var response = await _httpClient.GetAsync(commandPath, HttpCompletionOption.ResponseContentRead, cancellationSource.Token).ConfigureAwait(false);
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
                consulResult.Error = consulResult.ConsulUnreachable(ex);
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
            var versionTag = serviceEntry.Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            var version = versionTag?.Substring(versionPrefix.Length);

            return new ConsulNode(serviceEntry.Node.Name, serviceEntry.Service?.Port, version);
        }



        private static ulong? TryGetConsulIndex(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("x-consul-index", out var consulIndexHeaders))
            {
                ulong.TryParse(consulIndexHeaders.FirstOrDefault(), out var consulIndexValue);
                return consulIndexValue;
            }

            return null;
        }



        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
            try
            {
                _httpClient.Dispose();
            }
            catch (Exception e)
            {
                
            }
         
        }
    }
}