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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.SharedLogic.Utils;
using Metrics;
using Newtonsoft.Json;
using Timer = Metrics.Timer;

using static Gigya.Microdot.LanguageExtensions.MiscExtensions;

namespace Gigya.Microdot.ServiceProxy
{
    public class ServiceProxyProvider : IDisposable, IServiceProxyProvider
    {
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.None
        };

        /// <summary>
        /// Gets or sets default port used to access the remote service, it overridden by service discovery.
        /// </summary>
        public int? DefaultPort { get; set; }

        /// <summary>
        /// Gets the name of the remote service from the interface name.
        /// is used.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// GetObject a value indicating if a secure will be used to connect to the remote service. This defaults to the
        /// value that was specified in the <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>, overridden
        /// by service discovery.
        /// </summary>
        public bool ServiceInterfaceRequiresHttps { get; set; }


        /// <summary>
        /// Specifies a delegate that can be used to change a request in a user-defined way before it is sent over the
        /// network.
        /// </summary>
        public Action<HttpServiceRequest> PrepareRequest { get; set; }
        [Obsolete]
        public ISourceBlock<string> EndPointsChanged => null;
        [Obsolete]
        public ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged => null;
        private TimeSpan? Timeout { get; set; }

        internal IMultiEnvironmentServiceDiscovery ServiceDiscovery { get; set; }

        private readonly Timer _serializationTime;
        private readonly Timer _deserializationTime;
        private readonly Timer _roundtripTime;

        private readonly Counter _successCounter;
        private readonly Counter _failureCounter;
        /// <summary>Counts fatal errors with remote hosts, that cause us to disconnect from it.</summary>
        private readonly Counter _hostFailureCounter;
        private readonly Counter _applicationExceptionCounter;

        private HttpMessageHandler _httpMessageHandler = null;

        private readonly MetricsContext _connectionContext;

        private DateTime _lastHttpsTestTime = DateTime.MinValue;
        private readonly TimeSpan _httpsTestInterval;

        private readonly Func<HttpClientConfiguration, HttpMessageHandler> _httpMessageHandlerFactory;

        public const string METRICS_CONTEXT_NAME = "ServiceProxy";

        private ILog Log { get; }
        private ServiceDiscoveryConfig GetConfig() => GetDiscoveryConfig().Services[ServiceName];
        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }
        private JsonExceptionSerializer ExceptionSerializer { get; }

        private IEventPublisher<ClientCallEvent> EventPublisher { get; }

        private object HttpClientLock { get; } = new object();
        private HttpClient LastHttpClient { get; set; }
        private HttpClientConfiguration? LastHttpClientKey { get; set; }

        private bool Disposed { get; set; }

        private CurrentApplicationInfo AppInfo { get; }

        private static long _outstandingSentRequests;

        private ObjectPool<Stopwatch> _stopwatchPool = new ObjectPool<Stopwatch>(() => new Stopwatch(), 4096);

        public ServiceProxyProvider(string serviceName, IEventPublisher<ClientCallEvent> eventPublisher,
            ILog log,
            Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery> serviceDiscoveryFactory,
            Func<DiscoveryConfig> getConfig,
            JsonExceptionSerializer exceptionSerializer, 
            CurrentApplicationInfo appInfo,
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory)
        {
            EventPublisher = eventPublisher;

            Log = log;

            ServiceName = serviceName;
            GetDiscoveryConfig = getConfig;
            ExceptionSerializer = exceptionSerializer;
            AppInfo = appInfo;

            var metricsContext = Metric.Context(METRICS_CONTEXT_NAME).Context(ServiceName);
            _serializationTime = metricsContext.Timer("Serialization", Unit.Calls);
            _deserializationTime = metricsContext.Timer("Deserialization", Unit.Calls);
            _roundtripTime = metricsContext.Timer("Roundtrip", Unit.Calls);

            _successCounter = metricsContext.Counter("Success", Unit.Calls);
            _failureCounter = metricsContext.Counter("Failed", Unit.Calls);
            _hostFailureCounter = metricsContext.Counter("HostFailure", Unit.Calls);
            _applicationExceptionCounter = metricsContext.Counter("ApplicationException", Unit.Calls);

            _httpMessageHandlerFactory = messageHandlerFactory;

            ServiceDiscovery = serviceDiscoveryFactory(serviceName, ValidateReachability);

            var globalConfig = GetDiscoveryConfig();
            var serviceConfig = GetConfig();
            _httpsTestInterval = TimeSpan.FromMinutes(serviceConfig.TryHttpsIntervalInMinutes?? globalConfig.TryHttpsIntervalInMinutes);

            _connectionContext = Metric.Context("Connections");
        }

        /// <summary>
        /// Sets the length of time to wait for a HTTP request before aborting the request.
        /// </summary>
        /// <param name="timeout">The maximum length of time to wait.</param>
        public void SetHttpTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
        }

        private (HttpClient httpClient, bool isHttps, bool isNewCreated) GetHttpClient(ServiceDiscoveryConfig serviceConfig, DiscoveryConfig defaultConfig, bool tryHttps, string hostname, int basePort)
        {
            var forceHttps = serviceConfig.UseHttpsOverride ?? (ServiceInterfaceRequiresHttps || defaultConfig.UseHttpsOverride);
            var useHttps = tryHttps || forceHttps;
            string securityRole = serviceConfig.SecurityRole;
            var verificationMode = serviceConfig.ServerCertificateVerification ??
                                   defaultConfig.ServerCertificateVerification;
            var supplyClientCertificate = (serviceConfig.ClientCertificateVerification ?? defaultConfig.ClientCertificateVerification)
                                          == ClientCertificateVerificationMode.VerifyIdenticalRootCertificate;
            var httpKey = new HttpClientConfiguration(useHttps, securityRole, serviceConfig.RequestTimeout, verificationMode, supplyClientCertificate);

            lock (HttpClientLock)
            {
                if (LastHttpClient != null && LastHttpClientKey.Equals(httpKey))
                    return ( httpClient: LastHttpClient, isHttps: useHttps, isNewCreated: false);

                // In case we're trying HTTPs and the previous request on this instance was HTTP (or if this is the first request)
                if (Not(forceHttps) && httpKey.UseHttps && Not(LastHttpClientKey?.UseHttps ?? false))
                {
                    var now = DateTime.Now;
                    if (now - _lastHttpsTestTime > _httpsTestInterval)
                    {
                        _lastHttpsTestTime = now;
                        RunHttpsAvailabilityTest(httpKey, hostname, basePort);
                    }

                    httpKey = new HttpClientConfiguration(
                        useHttps: false, 
                        securityRole: null, 
                        timeout:httpKey.Timeout, 
                        verificationMode:httpKey.VerificationMode,
                        supplyClientCertificate: httpKey.SupplyClientCertificate);
                }


                var isNewCreated = false;
                if (!(LastHttpClientKey?.Equals(httpKey) ?? false))
                {
                    isNewCreated = true;
                    var equalState = (LastHttpClientKey?.Equals(httpKey))?.ToString() ?? "FirstTime";
                    Log.Info(msg => msg("GetHttpClient created new HttpClient", unencryptedTags: new Tags
                    {
                        {"debug.delay.newHttpClient", equalState},
                        {"debug.delay.method", "GetHttpClient"}
                    })); // we expect this not to be equal ever

                    var messageHandler = _httpMessageHandlerFactory(httpKey);
                    var httpClient = CreateHttpClient(messageHandler, httpKey.Timeout);

                    LastHttpClient = httpClient;
                    LastHttpClientKey = httpKey;
                    _httpMessageHandler = messageHandler;
                }

                return (httpClient: LastHttpClient, isHttps: httpKey.UseHttps, isNewCreated: isNewCreated);
            }
        }

        private HttpClient CreateHttpClient(HttpMessageHandler messageHandler, TimeSpan? requestTimeout)
        {
            var httpClient = new HttpClient(messageHandler);
            TimeSpan? timeout = Timeout ?? requestTimeout;
            if (timeout.HasValue)
                httpClient.Timeout = timeout.Value;

            return httpClient;
        }

        private async Task RunHttpsAvailabilityTest(HttpClientConfiguration configuration, string hostname, int basePort)
        {
            try
            {
                var clientConfiguration = new HttpClientConfiguration(
                    useHttps:true,
                    configuration.SecurityRole,
                    configuration.Timeout,
                    configuration.VerificationMode,
                    configuration.SupplyClientCertificate
                    );
                HttpMessageHandler messageHandler = null;
                HttpClient httpsClient = null;
                await ValidateReachability(hostname, basePort, fallbackOnProtocolError: false, clientFactory: _ =>
                {
                    messageHandler = _httpMessageHandlerFactory(clientConfiguration);
                    httpsClient = CreateHttpClient(messageHandler, configuration.Timeout);
                    return (httpsClient, isHttps: true, isNewCreated: true); // the return value is ignored as this async task is not awaited
                }, cancellationToken: CancellationToken.None);

                lock (HttpClientLock)
                {
                    var isEqual = LastHttpClient?.Equals(httpsClient).ToString() ?? "Null";
                    Log.Info(msg => msg("GetHttpClient created new HttpClient", unencryptedTags: new Tags
                    {
                        {"debug.delay.newHttpClient", isEqual},
                        {"debug.delay.method", "Reachability"}
                    })); // we expect this not to be equal ever

                    LastHttpClient = httpsClient;
                    LastHttpClientKey = new HttpClientConfiguration(
                        useHttps: true, 
                        configuration.SecurityRole, 
                        configuration.Timeout,
                        configuration.VerificationMode,
                        configuration.SupplyClientCertificate);
                    _httpMessageHandler = messageHandler;
                }
            }
            catch (HttpRequestException)
            {
                Log.Info(_ => _($"HTTPS for service {ServiceName} is not available."));
            }
        }

        private Task ValidateReachability(Node node, CancellationToken cancellationToken)
        {
            var config = GetConfig();
            var port = GetEffectivePort(node, config);
            if (port == null)
                throw new Exception("No port is configured");

            Func<bool, (HttpClient client, bool isHttps, bool isNewCreated)> clientFactory = tryHttps  => GetHttpClient(config, GetDiscoveryConfig(), tryHttps, node.Hostname, port.Value);
            return ValidateReachability(node.Hostname, port.Value, fallbackOnProtocolError: true, clientFactory: clientFactory, cancellationToken: cancellationToken);
        }

        private async Task ValidateReachability(string hostname,
            int port,
            bool fallbackOnProtocolError,
            Func<bool, (HttpClient httpClient, bool isHttps, bool isNewCreated)> clientFactory,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            (HttpClient httpClient, bool isHttps, bool isNewCreated)? clientInfo = null;
            try
            {
                clientInfo = clientFactory(true);
                var uri = BuildUri(hostname, port, clientInfo.Value.isHttps, out int _);

                response = await clientInfo.Value.httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
                when (fallbackOnProtocolError && !ServiceInterfaceRequiresHttps && (clientInfo?.isHttps ?? false) && (ex.InnerException as WebException)?.Status == WebExceptionStatus.ProtocolError)
            {
                var uri = BuildUri(hostname, port, false, out int _);
                response = await clientFactory(false)
                    .httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            }

            bool headerExists = response.Headers.Contains(GigyaHttpHeaders.ServerHostname);

            if (!headerExists)
            {
                throw new Exception($"The response does not contain {GigyaHttpHeaders.ServerHostname} header");
            }
        }

        private string BuildUri(string hostName, int basePort, bool useHttps, out int effectivePort)
        {
            string urlTemplate;
            if (useHttps)
            {
                urlTemplate = "https://{0}:{1}/";
                effectivePort = ServiceInterfaceRequiresHttps ? basePort : basePort + (int)PortOffsets.Https;
            }
            else
            {
                urlTemplate = "http://{0}:{1}/";
                effectivePort = basePort;
            }
            return string.Format(urlTemplate, hostName, effectivePort);
        }

        private int? GetEffectivePort(Node node, ServiceDiscoveryConfig config)
        {
            return node.Port ?? DefaultPort ?? config.DefaultPort;
        }


        public virtual Task<object> Invoke(HttpServiceRequest request, Type resultReturnType)
        {
            return Invoke(request, resultReturnType, JsonSettings);
        }

        public virtual async Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings)
        {
            using (_roundtripTime.NewContext())
                return await InvokeCore(request, resultReturnType, jsonSettings).ConfigureAwait(false);
        }

        private async Task<object> InvokeCore(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            request.TracingData = new TracingData
            {
                HostName = CurrentApplicationInfo.HostName?.ToUpperInvariant(),
                ServiceName = AppInfo.Name,
                RequestID = TracingContext.TryGetRequestID(),
                SpanID = Guid.NewGuid().ToString("N"), //Each call is new span                
                ParentSpanID = TracingContext.TryGetParentSpanID(),
                SpanStartTime = DateTimeOffset.UtcNow,
                AbandonRequestBy = TracingContext.AbandonRequestBy,
                AdditionalProperties = TracingContext.AdditionalProperties,
                Tags = TracingContext.TagsOrNull?.Tags,
            };
            PrepareRequest?.Invoke(request);

            var discoveryConfig = GetDiscoveryConfig();

            // Using non https connection only allowed if the service was configured to use http.
            // This will be important to decide whether downgrading from https to http is allowed
            // (in cases when the service contellation is transitioning to https and rollbacks are expected).
            var allowNonHttps = Not(GetConfig().UseHttpsOverride ?? discoveryConfig.UseHttpsOverride || ServiceInterfaceRequiresHttps);

            //will try using HTTPs only in-case we configured to try HTTPs explicitly
            bool tryHttps = GetConfig().TryHttps ?? discoveryConfig.TryHttps;

            var retriesCount = 0;
            while (true)
            {
                var config = GetConfig();
                var clientCallEvent = EventPublisher.CreateEvent();
                clientCallEvent.TargetService = ServiceName;
                clientCallEvent.RequestId = request.TracingData?.RequestID;
                clientCallEvent.TargetMethod = request.Target.MethodName;
                clientCallEvent.SpanId = request.TracingData?.SpanID;
                clientCallEvent.ParentSpanId = request.TracingData?.ParentSpanID;
                clientCallEvent.SuppressCaching = TracingContext.CacheSuppress;

                if (retriesCount > 0)
                    clientCallEvent.StatsRetryCount = retriesCount;
                
                retriesCount++;

                string responseContent;
                HttpResponseMessage response;
                var nodeAndLoadBalancer = await ServiceDiscovery.GetNode().ConfigureAwait(false); // can throw

                int? effectivePort = GetEffectivePort(nodeAndLoadBalancer.Node, config);
                if (effectivePort == null)
                    throw new ConfigurationException("Cannot access service. Service Port not configured. See tags to find missing configuration", unencrypted: new Tags {
                        {"ServiceName", ServiceName },
                        {"Required configuration key", $"Discovery.{ServiceName}.DefaultPort"}
                    });

                string uri = null;
                HttpClient httpClient = null;
                bool isHttps = false;
                try
                {
                    var cacheSuppresOverride = TracingContext.CacheSuppress == CacheSuppress.RecursiveAllDownstreamServices ?
                        CacheSuppress.RecursiveAllDownstreamServices :
                       (CacheSuppress?) null;

                    request.Overrides = TracingContext.TryGetOverrides()?.ShallowCloneWithOverrides(nodeAndLoadBalancer.PreferredEnvironment, cacheSuppresOverride)
                                        ?? new RequestOverrides
                                        {
                                            PreferredEnvironment  = nodeAndLoadBalancer.PreferredEnvironment, 
                                            SuppressCaching       = cacheSuppresOverride
                                        };

                    string requestContent = _serializationTime.Time(() => JsonConvert.SerializeObject(request, jsonSettings));

                    var httpContent = new StringContent(requestContent, Encoding.UTF8, "application/json");
                    
                    if (requestContent.Length > 10 * 1024)
                        clientCallEvent.Above10KmsgLength = requestContent.Length;

                    httpContent.Headers.Add(GigyaHttpHeaders.ProtocolVersion, HttpServiceRequest.ProtocolVersion);

                    var asyncActionStopwatch = _stopwatchPool.Get();
                    var outstandingSentReqs = Interlocked.Increment(ref _outstandingSentRequests);
                    clientCallEvent.RequestStartTimestamp = Stopwatch.GetTimestamp();
                    try
                    {
                        bool isNewCreated;
                        (httpClient, isHttps, isNewCreated) = GetHttpClient(config, discoveryConfig, tryHttps,
                            nodeAndLoadBalancer.Node.Hostname, effectivePort.Value);
                        
                        clientCallEvent.IsNewClientCreated = isNewCreated;
                        clientCallEvent.ProtocolSchema = isHttps ? "HTTPS" : "HTTP";

                        // The URL is only for a nice experience in Fiddler, it's never parsed/used for anything.
                        uri = BuildUri(nodeAndLoadBalancer.Node.Hostname, effectivePort.Value, isHttps,
                            out int actualPort) + ServiceName;
                        if (request.Target.MethodName != null)
                            uri += $".{request.Target.MethodName}";
                        if (request.Target.Endpoint != null)
                            uri += $"/{request.Target.Endpoint}";

                        Log.Debug(_ => _("ServiceProxy: Calling remote service. See tags for details.",
                            unencryptedTags: new
                            {
                                remoteEndpoint = nodeAndLoadBalancer.Node.Hostname,
                                remotePort = actualPort,
                                remoteServiceName = ServiceName,
                                remoteMethodName = request.Target.MethodName
                            }));

                        clientCallEvent.TargetHostName = nodeAndLoadBalancer.Node.Hostname;
                        clientCallEvent.TargetEnvironment = nodeAndLoadBalancer.TargetEnvironment;
                        clientCallEvent.TargetPort = actualPort;

                        clientCallEvent.OutstandingSentRequests = outstandingSentReqs;


                        var now = DateTime.UtcNow.Ticks;

                        asyncActionStopwatch.Restart();
                        response = await httpClient.PostAsync(uri, httpContent).ConfigureAwait(false);
                        asyncActionStopwatch.Stop();

                        clientCallEvent.StatsNetworkPostTime = asyncActionStopwatch.ElapsedMilliseconds;
                        clientCallEvent.PostDateTicks = now;

                        asyncActionStopwatch.Restart();
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        asyncActionStopwatch.Stop();

                        clientCallEvent.ClientReadResponseTime = asyncActionStopwatch.ElapsedMilliseconds;
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _outstandingSentRequests);
                        _stopwatchPool.Return(asyncActionStopwatch);

                        clientCallEvent.ResponseEndTimestamp = Stopwatch.GetTimestamp();
                        
                        PublishServiceConnectionMetrics(uri);
                    }
                    if (response.Headers.TryGetValues(GigyaHttpHeaders.ExecutionTime, out IEnumerable<string> values))
                    {
                        var time = values.FirstOrDefault();
                        if (TimeSpan.TryParse(time, out TimeSpan executionTime))
                        {
                            clientCallEvent.ServerTimeMs = executionTime.TotalMilliseconds;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    //In case we get any https request exception and we were trying HTTPs we must fallback to HTTP
                    //otherwise we will be stuck trying HTTPs because we didn't change the Http client and will probably 
                    //get different error than Tls errors
                    if (allowNonHttps && isHttps)
                    {
                        tryHttps = false;
                        continue;
                    }

                    Log.Error("The remote service failed to return a valid HTTP response. Continuing to next " +
                              "host. See tags for URL and exception for details.",
                        exception: ex,
                        unencryptedTags: new { uri });
                    _hostFailureCounter.Increment("RequestFailure");
                    clientCallEvent.Exception = ex;
                    EventPublisher.TryPublish(clientCallEvent); // fire and forget!

                    if (nodeAndLoadBalancer.LoadBalancer != null)
                    {
                        nodeAndLoadBalancer.LoadBalancer.ReportUnreachable(nodeAndLoadBalancer.Node, ex);
                        continue;
                    }

                    throw;
                }
                catch (TaskCanceledException ex)
                {
                    _failureCounter.Increment("RequestTimeout");

                    Exception rex = new RemoteServiceException("The request to the remote service exceeded the " +
                                                               "allotted timeout. See the 'RequestUri' property on this exception for the URL that was " +
                                                               "called and the tag 'requestTimeout' for the configured timeout.",
                        uri,
                        ex,
                        unencrypted: new Tags
                        {
                            {"requestTimeout", LastHttpClient?.Timeout.ToString()},
                            {"requestUri", uri}
                        });

                    clientCallEvent.Exception = rex;

                    EventPublisher.TryPublish(clientCallEvent); // fire and forget!
                    throw rex;
                }

                if (response.Headers.Contains(GigyaHttpHeaders.ServerHostname) || response.Headers.Contains(GigyaHttpHeaders.ProtocolVersion))
                {
                    try
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var returnObj = _deserializationTime.Time(() => JsonConvert.DeserializeObject(responseContent, resultReturnType, jsonSettings));

                            clientCallEvent.ErrCode = 0;
                            EventPublisher.TryPublish(clientCallEvent); // fire and forget!
                            _successCounter.Increment();

                            return returnObj;
                        }

                        // Failuire:
                        Exception remoteException;
                        try
                        {
                            remoteException = _deserializationTime.Time(() => ExceptionSerializer.Deserialize(responseContent));
                        }
                        catch (Exception ex)
                        {
                            _applicationExceptionCounter.Increment("ExceptionDeserializationFailure");

                            throw new RemoteServiceException("The remote service returned a failure response " +
                                                             "that failed to deserialize.  See the 'RequestUri' property on this exception " +
                                                             "for the URL that was called, the inner exception for the exact error and the " +
                                                             "'responseContent' encrypted tag for the original response content.",
                                uri,
                                ex,
                                unencrypted: new Tags { { "requestUri", uri } },
                                encrypted: new Tags { { "responseContent", responseContent } });
                        }

                        _applicationExceptionCounter.Increment();

                        clientCallEvent.Exception = remoteException;
                        EventPublisher.TryPublish(clientCallEvent); // fire and forget!

                        if (remoteException is RequestException || remoteException is EnvironmentException)
                            ExceptionDispatchInfo.Capture(remoteException).Throw();

                        if (remoteException is UnhandledException)
                            remoteException = remoteException.InnerException;

                        throw new RemoteServiceException("The remote service returned a failure response. See " +
                                                         "the 'RequestUri' property on this exception for the URL that was called, and the " +
                                                         "inner exception for details.",
                            uri,
                            remoteException,
                            unencrypted: new Tags { { "requestUri", uri } });
                    }
                    catch (JsonException ex)
                    {
                        _failureCounter.Increment("Serialization");

                        Log.Error("The remote service returned a response with JSON that failed " +
                                         "deserialization. See the 'uri' tag for the URL that was called, the exception for the " +
                                         "exact error and the 'responseContent' encrypted tag for the original response content.",
                                      exception: ex,
                                      unencryptedTags: new { uri },
                                      encryptedTags: new { responseContent });

                        clientCallEvent.Exception = ex;
                        EventPublisher.TryPublish(clientCallEvent); // fire and forget!
                        throw new RemoteServiceException("The remote service returned a response with JSON that " +
                                                         "failed deserialization. See the 'RequestUri' property on this exception for the URL " +
                                                         "that was called, the inner exception for the exact error and the 'responseContent' " +
                                                         "encrypted tag for the original response content.",
                            uri,
                            ex,
                            new Tags { { "responseContent", responseContent } },
                            new Tags { { "requestUri", uri } });
                    }
                }
                else
                {
                    if (allowNonHttps && isHttps)
                    {
						tryHttps = false;
						continue;
					}

                    var exception = response.StatusCode == HttpStatusCode.ServiceUnavailable ?
                        new Exception($"The remote service is unavailable (503) and is not recognized as a Gigya host at uri: {uri}") :
                        new Exception($"The remote service returned a response but is not recognized as a Gigya host at uri: {uri}");
                    if (nodeAndLoadBalancer.LoadBalancer == null)
                        throw exception;

                    nodeAndLoadBalancer.LoadBalancer.ReportUnreachable(nodeAndLoadBalancer.Node, exception);
                    _hostFailureCounter.Increment("NotGigyaHost");

                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        Log.Error("The remote service is unavailable (503) and is not recognized as a Gigya host. Continuing to next host.", unencryptedTags: new { uri });
                    else
                        Log.Error("The remote service returned a response but is not recognized as a Gigya host. Continuing to next host.", unencryptedTags: new { uri, statusCode = response.StatusCode }, encryptedTags: new { responseContent });

                    clientCallEvent.ErrCode = 500001;//(int)GSErrors.General_Server_Error;
                    EventPublisher.TryPublish(clientCallEvent); // fire and forget!
                }
            }
        }

        public async Task<ServiceSchema> GetSchema()
        {
            var result = await InvokeCore(new HttpServiceRequest { Target = new InvocationTarget { Endpoint = "schema" } }, typeof(ServiceSchema), JsonSettings).ConfigureAwait(false);
            return (ServiceSchema)result;
        }

        private void PublishServiceConnectionMetrics(string uri)
		{
			DiscoveryConfig config = GetDiscoveryConfig();
			if (!config.PublishConnectionsMetrics)
				return;

			Uri serviceUri = new Uri(uri);
			MetricsContext serviceContext = _connectionContext.Context(ServiceName);

			//[Connections - {ServiceName} - {Server-Host-Name}] Total (Connections)
			serviceContext.Context(serviceUri.Host).Gauge("Total", () =>
			{
				ServicePoint servicePoint = ServicePointManager.FindServicePoint(serviceUri);
				int currentConnections = servicePoint.CurrentConnections;
				if (currentConnections == 0)
					serviceContext.ShutdownContext(serviceUri.Host);

				return currentConnections;
			}, Unit.Custom("Connections"));

			//[Connections - {ServiceName} - {Server-Host-Name}] Port (Port)
			serviceContext.Context(serviceUri.Host).Gauge("Port", () => serviceUri.Port, Unit.Custom("Port"));

			//[Connections - {ServiceName}] Total (Connections)
			serviceContext.Gauge("Total", () =>
			{
				double sum = serviceContext.DataProvider.CurrentMetricsData.ChildMetrics
					.Where(m => !string.Equals(m.Context, "Total"))
					.Select(m => m.Gauges.FirstOrDefault(g => string.Equals(g.Name, "Total")))
					.Sum(g => g?.Value ?? 0);

				if (sum == 0)
					_connectionContext.ShutdownContext(ServiceName);

				return sum;
			}, Unit.Custom("Connections"));
		}

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                LastHttpClient?.Dispose();
                _httpMessageHandler?.Dispose();
            }

            Disposed = true;
        }
    }

}
