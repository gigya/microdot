﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.UnitTests.Discovery
{
    public sealed class ConsulSimulator: IDisposable
    {
        private readonly HttpListener _consulListener;

        private ConcurrentDictionary<string, List<ConsulEndPoint>> _serviceNodes;
        private ConcurrentDictionary<string, string> _serviceActiveVersion;

        private ulong _keyValueModifyIndex = 12345;
        private ulong _healthModifyIndex = 45678;

        private TaskCompletionSource<bool> _waitForKeyValueIndexModification;
        private TaskCompletionSource<bool> _waitForHealthIndexModification;
        private Exception _httpErrorFake;

        public ConsulSimulator(int consulPort)
        {
            Reset();

            var prefix = $"http://+:{consulPort}/";
            _consulListener = new HttpListener { Prefixes = { prefix } };
            try
            {
                _consulListener.Start();
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode != 5)
                    throw;

                throw new Exception(
                    "ConsulSimulator is trying to open a port to simulate Consul responses. The specified HTTP port wasn't configured to run without administrative permissions.\n" +
                    "To configure it, run the following command in an elevated (administrator) command prompt:\n" +
                    $"netsh http add urlacl url={prefix} user={CurrentApplicationInfo.OsUser}");
            }

            StartListening();
        }

        public void Reset()
        {
            _serviceNodes = new ConcurrentDictionary<string, List<ConsulEndPoint>>();
            _serviceActiveVersion = new ConcurrentDictionary<string, string>();
            _waitForHealthIndexModification = new TaskCompletionSource<bool>();
            _waitForKeyValueIndexModification = new TaskCompletionSource<bool>();
            _httpErrorFake = null;
        }

        private async Task<ConsulResponse> GetHealthResponse(string serviceName, ulong index)
        {
            if (!_serviceNodes.ContainsKey(serviceName))
                return new ConsulResponse{Content = "[]"};

            if (index >= _healthModifyIndex)
                await _waitForHealthIndexModification.Task;

            return new ConsulResponse{ ModifyIndex = _healthModifyIndex,  Content = 
            "[" + string.Join("\n,", _serviceNodes[serviceName].Select(ep =>
                @"{
                    ""Node"": {
                        ""Node"": """ + ep.HostName + @""",
                    },
                    ""Service"": {
                            ""Service"":""" + serviceName + @""",
                            ""Tags"": [""version:" + ep.Version + @"""],
                            ""Port"": " + ep.Port + @",
                    }
                }")) + 
            "]"};
        }

        private async Task<ConsulResponse> GetKeyValueResponse(string serviceName, ulong index)
        {
            if (!_serviceActiveVersion.ContainsKey(serviceName))
                return new ConsulResponse{StatusCode = HttpStatusCode.NotFound};

            if (index >= _keyValueModifyIndex)
                await _waitForKeyValueIndexModification.Task;

            return new ConsulResponse {ModifyIndex = _keyValueModifyIndex, Content =
                @"[
                    {
                        ""Key"": ""service/" + serviceName + @""",
                        ""Value"": """ + Convert.ToBase64String(Encoding.UTF8.GetBytes(@"{""version"" : """+_serviceActiveVersion[serviceName] + @"""}"))+ @"""
                    }
                  ]"
             };
        }

        private async Task<ConsulResponse> GetAllKeysResponse(string serviceName, ulong index)
        {
            if (index >= _keyValueModifyIndex)
                await _waitForKeyValueIndexModification.Task;

            return new ConsulResponse {ModifyIndex = _keyValueModifyIndex, Content =
                    "[" + string.Join(",", _serviceNodes.Keys) + "]"
            };
        }

        private async Task<ConsulResponse> GetQueryResponse(string serviceName, ulong index)
        {
            if (!_serviceNodes.ContainsKey(serviceName))
                return new ConsulResponse
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = "rpc error: rpc error: Query not found"
                };

            return new ConsulResponse {Content =
                @"{""Nodes"" : [" + string.Join("\n,", _serviceNodes[serviceName].Select(ep =>
                                @"{
                                        ""Node"": {
                                            ""Node"": """ + ep.HostName + @""",
                                        },
                                        ""Service"": {
                                                ""Service"":""" + serviceName + @""",
                                                ""Tags"": [""version:" + ep.Version + @"""],
                                                ""Port"": " + ep.Port + @",
                                        }
                                    }")) +
                             @"]
                    }"
            };
        }

        public void AddServiceEndpoint(string serviceName, ConsulEndPoint endPoint)
        {
            _serviceNodes.TryAdd(serviceName, new List<ConsulEndPoint>());
            _serviceNodes[serviceName].Add(endPoint);

            if (endPoint.Version!=null && !_serviceActiveVersion.TryGetValue(serviceName, out string _))
                SetServiceVersion(serviceName, endPoint.Version);

            IncreaseHealthModifyIndex();
        }

        public void RemoveServiceEndpoint(string serviceName, ConsulEndPoint endPoint)
        {            
            var index = _serviceNodes[serviceName].FindIndex(ep => ep.HostName==endPoint.HostName && ep.Port==endPoint.Port);
            if (index < 0)
                throw new Exception($"Endpoint not exists for service {serviceName}, cannot remove it");

            _serviceNodes[serviceName].RemoveAt(index);
            IncreaseHealthModifyIndex();
        }

        public void SetServiceVersion(string serviceName, string version)
        {
            _serviceActiveVersion.AddOrUpdate(serviceName, _ => version, (_,__)=> version);
            IncreaseKeyValueModifyIndex();
        }

        public void RemoveService(string serviceName)
        {
            _serviceNodes.TryRemove(serviceName, out List<ConsulEndPoint> _);
            IncreaseHealthModifyIndex();
            _serviceActiveVersion.TryRemove(serviceName, out string _);            
            IncreaseKeyValueModifyIndex();
        }

        public void SetError(Exception error)
        {
            _httpErrorFake = error;
        }

        private void IncreaseHealthModifyIndex()
        {
            _healthModifyIndex++;
            _waitForHealthIndexModification.TrySetResult(true);
            _waitForHealthIndexModification = new TaskCompletionSource<bool>();
        }

        private void IncreaseKeyValueModifyIndex()
        {
            _keyValueModifyIndex++;
            _waitForKeyValueIndexModification.TrySetResult(true);
            _waitForKeyValueIndexModification = new TaskCompletionSource<bool>();
        }

        private async void StartListening()
        {
            while (_consulListener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await _consulListener.GetContextAsync();
                    HandleConsulRequest(context);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private async void HandleConsulRequest(HttpListenerContext context)
        {
            var urlPath = context.Request.Url.PathAndQuery;

            if (_httpErrorFake == null)
            {
                var responseByRegEx = new Dictionary<string, Func<string, ulong, Task<ConsulResponse>>>
                {
                    {@"\/health\/service\/(.+)\?(.+)index=(\d+)", GetHealthResponse},
                    {@"\/kv\/service\?(.*)keys(.+)index=(\d+)", GetAllKeysResponse},
                    {@"\/kv\/service\/(.+)\?(.+)index=(\d+)", GetKeyValueResponse},
                    {@"query\/(.+)\/execute", GetQueryResponse}
                };

                foreach (var r in responseByRegEx)
                {
                    var match = Regex.Match(urlPath, r.Key);
                    if (match.Success)
                    {
                        ulong index = 0;
                        var serviceName = match.Groups[1].Value;
                        var getResponse = r.Value;
                        var indexExists = match.Groups.Count >= 4 && ulong.TryParse(match.Groups[3].Value, out index);

                        await SetResponse(context, serviceName, indexExists ? (ulong?) index : null, getResponse);
                        return;
                    }
                }
            }

            context.Response.StatusCode = 500;
            var errorMessage = _httpErrorFake?.Message ?? $"ConsulSimulator not supports the request {urlPath}";
            await SetResponse(context, "",0, (_,__) => Task.FromResult(new ConsulResponse
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = errorMessage
            }));
        }

        private async Task SetResponse(HttpListenerContext context, string serviceName, ulong? index, Func<string, ulong, Task<ConsulResponse>> getResponseByService)
        {
            using (context.Response)
            {
                Exception exception = null;
                ConsulResponse response = null;
                try
                {
                    response = await getResponseByService(serviceName, index ?? 0);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                exception = exception ?? _httpErrorFake;
                if (exception != null)
                    response = new ConsulResponse
                    {
                        Content = exception.Message,
                        StatusCode = HttpStatusCode.InternalServerError
                    };

                context.Response.StatusCode = (int)response.StatusCode;
                if (response.ModifyIndex!=null)
                    context.Response.AddHeader("x-consul-index", response.ModifyIndex.Value.ToString());
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response.Content), 0, response.Content.Length);
            }
        }
        
        public void Dispose()
        {
            _consulListener.Close();
            ((IDisposable)_consulListener)?.Dispose();
            _waitForKeyValueIndexModification.TrySetResult(false);
            _waitForHealthIndexModification.TrySetResult(false);
        }
    }

    public class ConsulResponse
    {
        public string Content { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public ulong? ModifyIndex { get; set; }
    }


}