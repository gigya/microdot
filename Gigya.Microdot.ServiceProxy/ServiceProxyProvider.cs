using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Security;

using Metrics;

using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy
{
    public class ServiceProxyProvider : IDisposable, IServiceProxyProvider
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
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
        /// Gets the name of the remote service. This defaults to the friendly name that was specified in the
        /// <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>. If none were specified, the interface name
        /// is used.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Get a value indicating if a secure will be used to connect to the remote service. This defaults to the
        /// value that was specified in the <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>, overridden
        /// by service discovery.
        /// </summary>
        public bool UseHttpsDefault { get;  set; }


        /// <summary>
        /// Specifies a delegate that can be used to change a request in a user-defined way before it is sent over the
        /// network.
        /// </summary>
        public Action<HttpServiceRequest> PrepareRequest { get; set; }
        public ISourceBlock<string> EndPointsChanged => ServiceDiscovery.EndPointsChanged;
        public ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged => ServiceDiscovery.ReachabilityChanged;
        private TimeSpan? Timeout { get; set; }

        internal IServiceDiscovery ServiceDiscovery { get; set; }

        private readonly Timer _serializationTime;
        private readonly Timer _deserializationTime;
        private readonly Timer _roundtripTime;

        private readonly Counter _successCounter;
        private readonly Counter _failureCounter;
        /// <summary>Counts fatal errors with remote hosts, that cause us to disconnect from it.</summary>
        private readonly Counter _hostFailureCounter;
        private readonly Counter _applicationExceptionCounter;

        private HttpMessageHandler _httpMessageHandler = new WebRequestHandler();

        protected internal HttpMessageHandler HttpMessageHandler
        {
            get
            {
                lock (HttpClientLock)
                {
                    return _httpMessageHandler;
                }
            }
            set
            {
                lock (HttpClientLock)
                {
                    _httpMessageHandler = value;
                    LastHttpClient = null;
                }
            }
        }

        public const string METRICS_CONTEXT_NAME = "ServiceProxy";

        private ICertificateLocator CertificateLocator { get; }
 
        private ILog Log { get; }
        private ServiceDiscoveryConfig GetConfig() => GetDiscoveryConfig().Services[ServiceName];
        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }

        private IEventPublisher<ClientCallEvent> EventPublisher { get; }

        private object HttpClientLock { get; } = new object();
        private HttpClient LastHttpClient { get; set; }
        private Tuple<bool, string, TimeSpan?> LastHttpClientKey { get; set; }

        private bool Disposed { get; set; }

        public ServiceProxyProvider(string serviceName, IEventPublisher<ClientCallEvent> eventPublisher,
            ICertificateLocator certificateLocator,
            ILog log,
            Func<string, ReachabilityChecker, IServiceDiscovery> serviceDiscoveryFactory,
            Func<DiscoveryConfig> getConfig)
        {
            EventPublisher = eventPublisher;
            CertificateLocator = certificateLocator;
   
            Log = log;

            ServiceName = serviceName;
            GetDiscoveryConfig = getConfig;

            var metricsContext = Metric.Context(METRICS_CONTEXT_NAME).Context(ServiceName);
            _serializationTime = metricsContext.Timer("Serialization", Unit.Calls);
            _deserializationTime = metricsContext.Timer("Deserialization", Unit.Calls);
            _roundtripTime = metricsContext.Timer("Roundtrip", Unit.Calls);

            _successCounter = metricsContext.Counter("Success", Unit.Calls);
            _failureCounter = metricsContext.Counter("Failed", Unit.Calls);
            _hostFailureCounter = metricsContext.Counter("HostFailure", Unit.Calls);
            _applicationExceptionCounter = metricsContext.Counter("ApplicationException", Unit.Calls);

            ServiceDiscovery = serviceDiscoveryFactory(serviceName, IsReachable);
        }




        /// <summary>
        /// Sets the length of time to wait for a HTTP request before aborting the request.
        /// </summary>
        /// <param name="timeout">The maximum length of time to wait.</param>
        public void SetHttpTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
        }

        private HttpClient GetHttpClient(ServiceDiscoveryConfig config)
        {
            lock (HttpClientLock)
            {
                bool useHttps = config.UseHttpsOverride ?? UseHttpsDefault;
                string securityRole = config.SecurityRole;
                var httpKey = Tuple.Create(useHttps, securityRole, config.RequestTimeout);

                if (LastHttpClient != null && LastHttpClientKey.Equals(httpKey))
                    return LastHttpClient;

                if (useHttps)
                    InitHttps(securityRole);

                LastHttpClientKey = httpKey;
                LastHttpClient = new HttpClient(HttpMessageHandler);
                TimeSpan? timeout = Timeout ?? config.RequestTimeout;
                if (timeout.HasValue)
                    LastHttpClient.Timeout = timeout.Value;
                return LastHttpClient;
            }
        }


        private void InitHttps(string securityRole)
        {
            if (HttpMessageHandler == null)
                HttpMessageHandler = new WebRequestHandler();

            var wrh = HttpMessageHandler as WebRequestHandler;

            if (wrh == null)
                throw new ProgrammaticException("When using HTTPS in ServiceProxy, only WebRequestHandler is supported.", unencrypted: new Tags { { "HandlerType", HttpMessageHandler.GetType().FullName } });

            var clientCert = CertificateLocator.GetCertificate("Client");
            var clientRootCertHash = clientCert.GetHashOfRootCertificate();

            wrh.ClientCertificates.Add(clientCert);

            wrh.ServerCertificateValidationCallback = (sender, serverCertificate, serverChain, errors) =>
            {
                switch (errors)
                {
                    case SslPolicyErrors.RemoteCertificateNotAvailable:
                        Log.Error(e => e("Remote certificate not available."));
                        return false;
                    case SslPolicyErrors.RemoteCertificateChainErrors:
                        Log.Error(log =>
                                {
                                    var sb = new StringBuilder("Certificate error/s.");
                                    foreach (var chainStatus in serverChain.ChainStatus)
                                    {
                                        sb.AppendFormat("Status {0}, status information {1}\n", chainStatus.Status, chainStatus.StatusInformation);
                                    }
                                    log(sb.ToString());
                                });
                        return false;
                    case SslPolicyErrors.RemoteCertificateNameMismatch: // by design domain name do not match name of certificate, so RemoteCertificateNameMismatch is not an error.
                    case SslPolicyErrors.None:
                        //Check if security role of a server is as expected
                        if (securityRole != null)
                        {
                            var name = ((X509Certificate2)serverCertificate).GetNameInfo(X509NameType.SimpleName, false);

                            if (name == null || !name.Contains(securityRole))
                            {
                                return false;
                            }
                        }

                        bool hasSameRootCertificateHash = serverChain.HasSameRootCertificateHash(clientRootCertHash);

                        if (!hasSameRootCertificateHash)
                            Log.Error(_ => _("Server root certificate do not match client root certificate"));

                        return hasSameRootCertificateHash;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(errors), errors, "The supplied value of SslPolicyErrors is invalid.");
                }
            };
        }


        private async Task<bool> IsReachable(IEndPointHandle endpoint)
        {
            try
            {
                var config = GetConfig();
                var uri = BuildUri(endpoint, config);
                var response = await GetHttpClient(config).GetAsync(uri, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);

                return response.Headers.Contains(GigyaHttpHeaders.ServerHostname);
            }
            catch
            {
                return false;
            }
        }


        private string BuildUri(IEndPointHandle endpoint, ServiceDiscoveryConfig config)
        {
            var useHttps = config.UseHttpsOverride ?? UseHttpsDefault;
            var urlTemplate = useHttps ? "https://{0}:{1}/" : "http://{0}:{1}/";
            var port = endpoint.Port ?? DefaultPort ?? config.DefaultPort;
            if (port==null)
                throw new ConfigurationException("Cannot access service. Service Port not configured. See tags to find missing configuration", unencrypted: new Tags
                {
                    {"ServiceName", ServiceName },
                    {"Required configuration key", $"Discovery.{ServiceName}.DefaultPort"}
                });
            return string.Format(urlTemplate, endpoint.HostName, port);
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
            if(request == null)
                throw new ArgumentNullException(nameof(request));
            request.Overrides = TracingContext.TryGetOverrides();
            request.TracingData = new TracingData
            {
                HostName = CurrentApplicationInfo.HostName?.ToUpperInvariant(),
                ServiceName = CurrentApplicationInfo.Name,
                RequestID = TracingContext.TryGetRequestID(),
                SpanID = Guid.NewGuid().ToString("N"), //Each call is new span                
                ParentSpanID = TracingContext.TryGetSpanID()
            };
            PrepareRequest?.Invoke(request);
            var requestContent = _serializationTime.Time(() => JsonConvert.SerializeObject(request, jsonSettings));

            var serviceEvent = EventPublisher.CreateEvent();
            serviceEvent.TargetService = ServiceName;
            serviceEvent.TargetMethod = request.Target.MethodName;
            serviceEvent.RequestId = request.TracingData?.RequestID;
            serviceEvent.SpanId = request.TracingData?.SpanID;
            serviceEvent.ParentSpanId = request.TracingData?.ParentSpanID;

            var config = GetConfig();

            while (true)
            {
                string responseContent;
                HttpResponseMessage response;
                IEndPointHandle endPoint = await ServiceDiscovery.GetNextHost(serviceEvent.RequestId);

                // The URL is only for a nice experience in Fiddler, it's never parsed/used for anything.
                var uri = string.Format(BuildUri(endPoint, config) + ServiceName);
                if (request.Target.MethodName!=null)
                    uri += $".{request.Target.MethodName}";
                if (request.Target.Endpoint != null)
                    uri += $"/{request.Target.Endpoint}";

                try
                {
                    Log.Debug(_ => _("ServiceProxy: Calling remote service. See tags for details.",
                                  unencryptedTags: new
                                  {
                                      remoteEndpoint = endPoint.HostName,
                                      remotePort = endPoint.Port ?? DefaultPort,
                                      remoteServiceName = ServiceName,
                                      remoteMethodName = request.Target.MethodName
                                  }));

                    serviceEvent.TargetHostName = endPoint.HostName;                    
                    var httpContent = new StringContent(requestContent, Encoding.UTF8, "application/json");
                    httpContent.Headers.Add(GigyaHttpHeaders.Version, HttpServiceRequest.Version);

                    serviceEvent.RequestStartTimestamp = Stopwatch.GetTimestamp();
                    try
                    {
                        response = await GetHttpClient(config).PostAsync(uri, httpContent).ConfigureAwait(false);
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        serviceEvent.ResponseEndTimestamp = Stopwatch.GetTimestamp();
                    }
                    IEnumerable<string> values;
                    if(response.Headers.TryGetValues(GigyaHttpHeaders.ExecutionTime, out values))
                    {
                        var time = values.FirstOrDefault();
                        TimeSpan executionTime;
                        if (TimeSpan.TryParse(time, out executionTime))
                        {
                            serviceEvent.ServerTimeMs = executionTime.TotalMilliseconds;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log.Error("The remote service failed to return a valid HTTP response. Continuing to next " +
                              "host. See tags for URL and exception for details.",
                        exception: ex,
                        unencryptedTags: new {uri});

                    endPoint.ReportFailure(ex);
                    _hostFailureCounter.Increment("RequestFailure");
                    serviceEvent.Exception = ex;
                    EventPublisher.TryPublish(serviceEvent); // fire and forget!
                    continue;
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

                    serviceEvent.Exception = rex;

                    EventPublisher.TryPublish(serviceEvent); // fire and forget!
                    throw rex;
                }

                if (response.Headers.Contains(GigyaHttpHeaders.ServerHostname) || response.Headers.Contains(GigyaHttpHeaders.Version))
                {
                    try
                    {
                        endPoint.ReportSuccess();

                        if(response.IsSuccessStatusCode)
                        {
                            var returnObj = _deserializationTime.Time(() => JsonConvert.DeserializeObject(responseContent, resultReturnType, jsonSettings));

                            serviceEvent.ErrCode = 0;
                            EventPublisher.TryPublish(serviceEvent); // fire and forget!
                            _successCounter.Increment();

                            return returnObj;
                        }
                        else
                        {
                            Exception remoteException;

                            try
                            {
                                remoteException = _deserializationTime.Time(() => JsonExceptionSerializer.Deserialize(responseContent));
                            }
                            catch(Exception ex)
                            {
                                _applicationExceptionCounter.Increment("ExceptionDeserializationFailure");

                                throw new RemoteServiceException("The remote service returned a failure response " +
                                                                 "that failed to deserialize.  See the 'RequestUri' property on this exception " +
                                                                 "for the URL that was called, the inner exception for the exact error and the " +
                                                                 "'responseContent' encrypted tag for the original response content.",
                                    uri,
                                    ex,
                                    unencrypted: new Tags {{"requestUri", uri}},
                                    encrypted: new Tags {{"responseContent", responseContent}});
                            }

                            _applicationExceptionCounter.Increment();

                            serviceEvent.Exception = remoteException;
                            EventPublisher.TryPublish(serviceEvent); // fire and forget!

                            if(remoteException is RequestException || remoteException is EnvironmentException)
                                ExceptionDispatchInfo.Capture(remoteException).Throw();

                            if(remoteException is UnhandledException)
                                remoteException = remoteException.InnerException;

                            throw new RemoteServiceException("The remote service returned a failure response. See " +
                                                             "the 'RequestUri' property on this exception for the URL that was called, and the " +
                                                             "inner exception for details.",
                                uri,
                                remoteException,
                                unencrypted: new Tags {{"requestUri", uri}});
                        }
                    }
                    catch (JsonException ex)
                    {
                        _failureCounter.Increment("Serialization");

                        Log.Error(_ => _("The remote service returned a response with JSON that failed " +
                                         "deserialization. See the 'uri' tag for the URL that was called, the exception for the " +
                                         "exact error and the 'responseContent' encrypted tag for the original response content.",
                                      exception: ex,
                                      unencryptedTags: new {uri},
                                      encryptedTags: new {responseContent}));

                        serviceEvent.Exception = ex;
                        EventPublisher.TryPublish(serviceEvent); // fire and forget!
                        throw new RemoteServiceException("The remote service returned a response with JSON that " +
                                                         "failed deserialization. See the 'RequestUri' property on this exception for the URL " +
                                                         "that was called, the inner exception for the exact error and the 'responseContent' " +
                                                         "encrypted tag for the original response content.",
                            uri,
                            ex,
                            new Tags {{"responseContent", responseContent}},
                            new Tags {{"requestUri", uri}});
                    }
                }
                else
                {
                    var exception = response.StatusCode == HttpStatusCode.ServiceUnavailable ?
                        new Exception($"The remote service is unavailable (503) and is not recognized as a Gigya host at uri: {uri}"):
                        new Exception($"The remote service returned a response but is not recognized as a Gigya host at uri: {uri}");

                    endPoint.ReportFailure(exception);
                    _hostFailureCounter.Increment("NotGigyaHost");

                    if(response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        Log.Error(_ => _("The remote service is unavailable (503) and is not recognized as a Gigya host. Continuing to next host.", unencryptedTags: new {uri}));
                    else
                        Log.Error(_ => _("The remote service returned a response but is not recognized as a Gigya host. Continuing to next host.", unencryptedTags: new {uri, statusCode = response.StatusCode}, encryptedTags: new {responseContent}));

                    serviceEvent.ErrCode = 500001;//(int)GSErrors.General_Server_Error;
                    EventPublisher.TryPublish(serviceEvent); // fire and forget!
                }
            }
        }

        public async Task<ServiceSchema> GetSchema()
        {
            var result = await InvokeCore(new HttpServiceRequest {Target = new InvocationTarget {Endpoint = "schema"}}, typeof(ServiceSchema), JsonSettings).ConfigureAwait(false);
            return (ServiceSchema) result;
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
