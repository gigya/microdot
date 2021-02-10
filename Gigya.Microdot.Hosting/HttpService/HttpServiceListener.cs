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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Measurement;
using Gigya.Microdot.SharedLogic.Security;
using Gigya.Microdot.SharedLogic.Utils;
using Gigya.ServiceContract.Exceptions;
using Metrics;
using Newtonsoft.Json;
using Timer = Metrics.Timer;


// ReSharper disable ConsiderUsingConfigureAwait

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IRequestListener : IDisposable
    {
        Task Listen();
        void Stop();
    }

    public sealed class HttpServiceListener : IRequestListener
    {
        private readonly IServerRequestPublisher _serverRequestPublisher;

        private Task listeningTask;

        private static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.None
        };

        private static JsonSerializerSettings JsonSettingsWeak { get; } = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.None
        };
        private byte[] ServerRootCertHash { get; }

        private IActivator Activator { get; }
        private IWorker Worker { get; }
        private IServiceEndPointDefinition ServiceEndPointDefinition { get; }
        private HttpListener Listener { get; }
        private ILog Log { get; }
        private IEnumerable<ICustomEndpoint> CustomEndpoints { get; }
        private IEnvironment Environment { get; }
        private JsonExceptionSerializer ExceptionSerializer { get; }
        private Func<LoadShedding> LoadSheddingConfig { get; }
        private CurrentApplicationInfo AppInfo { get; }

        private ServiceSchema ServiceSchema { get; }

        private readonly Timer _serializationTime;
        private readonly Timer _deserializationTime;
        private readonly Timer _roundtripTime;
        private readonly Counter _successCounter;
        private readonly Counter _failureCounter;
        private readonly Timer _activeRequestsCounter;
        private readonly Timer _metaEndpointsRoundtripTime;
        private readonly MetricsContext _endpointContext;
        private DataAnnotationsValidator _validator = new DataAnnotationsValidator();
        private TaskCompletionSource<int> _ReadyToGetTraffic = new TaskCompletionSource<int>();
        private readonly bool _extendedDelayTimeLogging = false;

        public void StartGettingTraffic()
        {
            _ReadyToGetTraffic.TrySetResult(1);
        }

        public HttpServiceListener(
            IActivator activator,
            IWorker worker,
            IServiceEndPointDefinition serviceEndPointDefinition,
            ICertificateLocator certificateLocator,
            ILog log,
            IEnumerable<ICustomEndpoint> customEndpoints,
            IEnvironment environment,
            JsonExceptionSerializer exceptionSerializer,
            ServiceSchema serviceSchema,
            Func<LoadShedding> loadSheddingConfig,
            IServerRequestPublisher serverRequestPublisher,
            CurrentApplicationInfo appInfo,
            Func<MicrodotHostingConfig> microdotHostingConfigFactory)
        {
            ServiceSchema = serviceSchema;
            _serverRequestPublisher = serverRequestPublisher;

            ServiceEndPointDefinition = serviceEndPointDefinition;
            Worker = worker;
            Activator = activator;
            Log = log;
            CustomEndpoints = customEndpoints.ToArray();
            Environment = environment;
            ExceptionSerializer = exceptionSerializer;
            LoadSheddingConfig = loadSheddingConfig;
            AppInfo = appInfo;

            _extendedDelayTimeLogging = microdotHostingConfigFactory().ExtendedDelaysTimeLogging; // no need to read every request

            if (ServiceEndPointDefinition.HttpsPort != null && ServiceEndPointDefinition.ClientCertificateVerification != ClientCertificateVerificationMode.Disable)
                ServerRootCertHash = certificateLocator.GetCertificate("Service").GetHashOfRootCertificate();

            Listener = new HttpListener { IgnoreWriteExceptions = true };
            if (ServiceEndPointDefinition.HttpsPort != null)
                Listener.Prefixes.Add($"https://+:{ServiceEndPointDefinition.HttpsPort}/");
            if (ServiceEndPointDefinition.HttpPort != null)
                Listener.Prefixes.Add($"http://+:{ServiceEndPointDefinition.HttpPort}/");
            if (!Listener.Prefixes.Any())
                Log.Warn(_ => _("HttpServiceListener is not listening on any ports, no HTTP or HTTPS ports in ServiceEndPointDefinition"));

            var context = Metric.Context("Service").Context(AppInfo.Name);
            _serializationTime = context.Timer("Serialization", Unit.Calls);
            _deserializationTime = context.Timer("Deserialization", Unit.Calls);
            _roundtripTime = context.Timer("Roundtrip", Unit.Calls);
            _metaEndpointsRoundtripTime = context.Timer("MetaRoundtrip", Unit.Calls);
            _successCounter = context.Counter("Success", Unit.Calls);
            _failureCounter = context.Counter("Failed", Unit.Calls);
            _activeRequestsCounter = context.Timer("ActiveRequests", Unit.Requests);
            _endpointContext = context.Context("Endpoints");
        }

        public Task Listen()
        {
            this.listeningTask = this.Start();

            this.StartGettingTraffic();

            return this.listeningTask;
        }

        public async Task Start()
        {
            try
            {
                Listener.Start();
                Log.Info(_ => _("HttpServiceListener started", unencryptedTags: new { prefixes = string.Join(",", Listener.Prefixes) }));
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode != 5)
                {
                    if (ServiceEndPointDefinition.HttpPort != null)
                        ex.Data["HttpPort"] = $"{ServiceEndPointDefinition.HttpPort}";
                    if (ServiceEndPointDefinition.HttpsPort != null)
                        ex.Data["HttpsPort"] = $"{ServiceEndPointDefinition.HttpsPort}";

                    ex.Data["Prefixes"] = Listener.Prefixes;
                    ex.Data["User"] = AppInfo.OsUser;
                    throw;
                }

                throw new Exception(
                    "One or more of the specified HTTP listen ports wasn't configured to run without administrative permissions.\n" +
                    "To configure them, run the following commands in an elevated (administrator) command prompt:\n" +
                    string.Join("\n", Listener.Prefixes.Select(prefix => $"netsh http add urlacl url={prefix} user={AppInfo.OsUser}")));
            }

            StartListening();
        }

        private static long _outstandingRecvRequests;
        private readonly ObjectPool<Stopwatch> _stopwatchPool = new ObjectPool<Stopwatch>(() => new Stopwatch(), 4096);

        private async Task StartListening()
        {

            await _ReadyToGetTraffic.Task;

            var sp = Stopwatch.StartNew();

            while (Listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    sp.Restart();

                    context = await Listener.GetContextAsync();

                    var timeFromLastReq = sp.ElapsedMilliseconds;
                    var ticks = DateTime.UtcNow.Ticks;
                    Interlocked.Increment(ref _outstandingRecvRequests);

                    Worker.FireAndForget(() => HandleRequest(context, ticks, timeFromLastReq));

                    var elapsed = sp.ElapsedMilliseconds;
                    if (_extendedDelayTimeLogging && elapsed > 1000)
                    {
                        Log.Info((t) => t("FireAndForget took more then 1 seconds", unencryptedTags: new Tags
                        {
                            {"debug.delay.fireAndForget", elapsed.ToString()}
                        }));
                    }
                }
                catch (ObjectDisposedException)
                {
                    break; // Listener has been stopped, GetContextAsync() is aborted.
                }
                catch (HttpListenerException ex)
                {
                    if (ex.ErrorCode == 995)
                        break;

                    Log.Error(_ => _("An error has occured during HttpListener.GetContextAsync(). Stopped listening to additional requests.", exception: ex));
                }
                catch (Exception ex)
                {
                    Log.Error(_ => _("An error has occured during HttpListener.GetContextAsync(). Stopped listening to additional requests.", exception: ex));
                    throw;
                }
            }
        }


        private async Task HandleRequest(HttpListenerContext context, long ticks, long timeFromLastReq)
        {
            var sw = _stopwatchPool.Get();
            try
            {
                var deltaDelayTicks = DateTime.UtcNow.Ticks - ticks;
                sw.Restart();
                RequestTimings.ClearCurrentTimings();
                using (context.Response)
                {
                    // Special endpoints should not be logged/measured/traced like regular endpoints
                    // Access is allowed without HTTPS verifications since they don't expose anything sensitive (e.g. config values are encrypted)
                    if (await TryHandleSpecialEndpoints(context)) return;

                    // Regular endpoint handling
                    using (_activeRequestsCounter.NewContext("Request"))
                    {
                        RequestTimings.GetOrCreate(); // initialize request timing context

                        string methodName = null;
                        // Initialize with empty object for protocol backwards-compatibility.

                        var requestData = new HttpServiceRequest { TracingData = new TracingData() };
                        object[] argumentsWithDefaults = null;
                        ServiceMethod serviceMethod = null;
                        ServiceCallEvent callEvent = _serverRequestPublisher.GetNewCallEvent();
                        try
                        {
                            try
                            {
                                await CheckSecureConnection(context);

                                ValidateRequest(context);

                                requestData = await ParseRequest(context);


                                //-----------------------------------------------------------------------------------------
                                // Don't move TracingContext writes main flow, IT have to be here, to avoid side changes
                                //-----------------------------------------------------------------------------------------
                                TracingContext.SetRequestID(requestData.TracingData.RequestID);
                                TracingContext.SpanStartTime = requestData.TracingData.SpanStartTime;
                                TracingContext.AbandonRequestBy = requestData.TracingData.AbandonRequestBy;
                                TracingContext.SetParentSpan(
                                    requestData.TracingData.SpanID ?? Guid.NewGuid().ToString("N"));
                                TracingContext.SetOverrides(requestData.Overrides);
                                if (requestData.TracingData.Tags != null)
                                    TracingContext.Tags = new ContextTags(requestData.TracingData.Tags);
                                TracingContext.AdditionalProperties = requestData.TracingData.AdditionalProperties;

                                callEvent.ServiceMethodSchema = context.Request.IsSecureConnection ? "HTTPS" : "HTTP";
                                SetCallEventRequestData(callEvent, requestData);

                                serviceMethod = ServiceEndPointDefinition.Resolve(requestData.Target);
                                callEvent.CalledServiceName = serviceMethod.GrainInterfaceType.Name;
                                methodName = serviceMethod.ServiceInterfaceMethod.Name;
                                var arguments = requestData.Target.IsWeaklyTyped
                                    ? GetParametersByName(serviceMethod, requestData.Arguments)
                                    : requestData.Arguments.Values.Cast<object>().ToArray();
                                argumentsWithDefaults =
                                    GetConvertedAndDefaultArguments(serviceMethod.ServiceInterfaceMethod, arguments);

                                if (_extendedDelayTimeLogging)
                                {
                                    callEvent.RecvDateTicks = ticks;
                                    callEvent.ReqStartupDeltaTicks = deltaDelayTicks;
                                    callEvent.TimeFromLastReq = timeFromLastReq;
                                    var outstandingRecvReqs = Interlocked.Read(ref _outstandingRecvRequests);
                                    callEvent.OutstandingRecvRequests = outstandingRecvReqs;
                                    if (deltaDelayTicks > 10_000_000)
                                    {
                                        callEvent.CollectionCountGen0 = GC.CollectionCount(0);
                                        callEvent.CollectionCountGen1 = GC.CollectionCount(1);
                                        callEvent.CollectionCountGen2 = GC.CollectionCount(2);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                callEvent.Exception = e;
                                if (e is RequestException)
                                    throw;

                                throw new RequestException("Invalid request", e);
                            }

                            RejectRequestIfLateOrOverloaded();

                            var responseJson = await GetResponse(context, serviceMethod, requestData,
                                argumentsWithDefaults);
                            if (await TryWriteResponse(context, responseJson, serviceCallEvent: callEvent))
                            {
                                callEvent.ErrCode = 0;
                                _successCounter.Increment();
                            }
                            else _failureCounter.Increment();
                        }
                        catch (Exception e)
                        {
                            callEvent.Exception = callEvent.Exception ?? e;
                            _failureCounter.Increment();
                            Exception ex = GetRelevantException(e);
                            string json = _serializationTime.Time(() => ExceptionSerializer.Serialize(ex));
                            await TryWriteResponse(context, json, GetExceptionStatusCode(ex),
                                serviceCallEvent: callEvent);
                        }
                        finally
                        {
                            sw.Stop();
                            callEvent.ActualTotalTime = sw.Elapsed.TotalMilliseconds;

                            _roundtripTime.Record((long)(sw.Elapsed.TotalMilliseconds * 1000000),
                                TimeUnit.Nanoseconds);
                            if (methodName != null)
                                _endpointContext.Timer(methodName, Unit.Requests)
                                    .Record((long)(sw.Elapsed.TotalMilliseconds * 1000000), TimeUnit.Nanoseconds);

                            _serverRequestPublisher.TryPublish(callEvent, argumentsWithDefaults, serviceMethod);
                        }
                    }
                }
            }
            finally
            {
                _stopwatchPool.Return(sw);
                Interlocked.Decrement(ref _outstandingRecvRequests);
            }
        }


        private static object[] GetConvertedAndDefaultArguments(MethodInfo method, object[] arguments)
        {
            var parameters = method.GetParameters();

            object[] argumentsWithDefaults = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                object argument = null;
                var param = parameters[i];

                if (i < arguments.Length)
                {
                    argument = JsonHelper.ConvertWeaklyTypedValue(arguments[i], param.ParameterType);
                }
                else
                {
                    if (param.IsOptional)
                    {
                        if (param.HasDefaultValue)
                            argument = param.DefaultValue;
                        else if (param.ParameterType.IsValueType)
                            argument = System.Activator.CreateInstance(param.ParameterType);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Call to method {method.Name} is missing argument for {param.Name} and this paramater is not optional.");
                    }
                }

                argumentsWithDefaults[i] = argument;
            }

            return argumentsWithDefaults;
        }

        private void SetCallEventRequestData(ServiceCallEvent callEvent, HttpServiceRequest requestData)
        {
            callEvent.ClientMetadata = requestData.TracingData;
            callEvent.ServiceMethod = requestData.Target?.MethodName;
            callEvent.RequestId = requestData.TracingData?.RequestID;
            callEvent.SpanId = requestData.TracingData?.SpanID;
            callEvent.ParentSpanId = requestData.TracingData?.ParentSpanID;
            callEvent.ContextUnencryptedTags = requestData.TracingData?.Tags?.GetUnencryptedTags();
            callEvent.ContextTagsEncrypted = requestData.TracingData?.Tags?.GetEncryptedTags();
            callEvent.UnknownTracingData = requestData.TracingData?.AdditionalProperties;
            callEvent.SuppressCaching = requestData.Overrides?.SuppressCaching;
        }

        private async Task<bool> TryHandleSpecialEndpoints(HttpListenerContext context)
        {
            try
            {
                foreach (var customEndpoint in CustomEndpoints)
                {
                    if (await customEndpoint.TryHandle(context,
                        (data, status, type) => TryWriteResponse(context, data, status, type)))
                    {
                        if (RequestTimings.Current.Request.ElapsedMS != null)
                            _metaEndpointsRoundtripTime.Record((long)RequestTimings.Current.Request.ElapsedMS,
                                TimeUnit.Milliseconds);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                var ex = GetRelevantException(e);
                await TryWriteResponse(context, ExceptionSerializer.Serialize(ex), GetExceptionStatusCode(ex));
                return true;
            }

            return false;
        }


        private void RejectRequestIfLateOrOverloaded()
        {
            var config = LoadSheddingConfig();
            var now = DateTimeOffset.UtcNow;

            // Too much time passed since our direct caller made the request to us; something's causing a delay. Log or reject the request, if needed.
            if (config.DropMicrodotRequestsBySpanTime != LoadShedding.Toggle.Disabled
                && TracingContext.SpanStartTime != null
                && TracingContext.SpanStartTime.Value + config.DropMicrodotRequestsOlderThanSpanTimeBy < now)
            {

                if (config.DropMicrodotRequestsBySpanTime == LoadShedding.Toggle.LogOnly)
                    Log.Warn(_ => _("Accepted Microdot request despite that too much time passed since the client sent it to us.", unencryptedTags: new
                    {
                        clientSendTime = TracingContext.SpanStartTime,
                        currentTime = now,
                        maxDelayInSecs = config.DropMicrodotRequestsOlderThanSpanTimeBy.TotalSeconds,
                        actualDelayInSecs = (now - TracingContext.SpanStartTime.Value).TotalSeconds,
                    }));

                else if (config.DropMicrodotRequestsBySpanTime == LoadShedding.Toggle.Drop)
                    throw new EnvironmentException("Dropping Microdot request since too much time passed since the client sent it to us.", unencrypted: new Tags
                    {
                        ["clientSendTime"] = TracingContext.SpanStartTime.ToString(),
                        ["currentTime"] = now.ToString(),
                        ["maxDelayInSecs"] = config.DropMicrodotRequestsOlderThanSpanTimeBy.TotalSeconds.ToString(),
                        ["actualDelayInSecs"] = (now - TracingContext.SpanStartTime.Value).TotalSeconds.ToString(),
                    });
            }

            // Too much time passed since the API gateway initially sent this request till it reached us (potentially
            // passing through other micro-services along the way). Log or reject the request, if needed.
            if (config.DropRequestsByDeathTime != LoadShedding.Toggle.Disabled
                && TracingContext.AbandonRequestBy != null
                && now > TracingContext.AbandonRequestBy.Value - config.TimeToDropBeforeDeathTime)
            {
                if (config.DropRequestsByDeathTime == LoadShedding.Toggle.LogOnly)
                    Log.Warn(_ => _("Accepted Microdot request despite exceeding the API gateway timeout.", unencryptedTags: new
                    {
                        requestDeathTime = TracingContext.AbandonRequestBy,
                        currentTime = now,
                        overTimeInSecs = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds,
                    }));

                else if (config.DropRequestsByDeathTime == LoadShedding.Toggle.Drop)
                    throw new EnvironmentException("Dropping Microdot request since the API gateway timeout passed.", unencrypted: new Tags
                    {
                        ["requestDeathTime"] = TracingContext.AbandonRequestBy.ToString(),
                        ["currentTime"] = now.ToString(),
                        ["overTimeInSecs"] = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds.ToString(),
                    });
            }
        }


        private static Exception GetRelevantException(Exception e)
        {
            if (e is RequestException)
                return e;

            var ex = GetAllExceptions(e).FirstOrDefault(x => (x is TargetInvocationException || x is AggregateException) == false);

            return ex;
        }

        private static IEnumerable<Exception> GetAllExceptions(Exception ex)
        {
            while (ex != null)
            {
                yield return ex;
                ex = ex.InnerException;
            }
        }

        private void ValidateRequest(HttpListenerContext context)
        {
            var clientVersion = context.Request.Headers[GigyaHttpHeaders.ProtocolVersion];

            if (clientVersion != null && clientVersion != HttpServiceRequest.ProtocolVersion)
            {
                _failureCounter.Increment("ProtocolVersionMismatch");
                throw new RequestException($"Client protocol version {clientVersion} is not supported by the server protocol version {HttpServiceRequest.ProtocolVersion}.");
            }

            if (context.Request.HttpMethod != "POST")
            {
                context.Response.Headers.Add("Allow", "POST");
                _failureCounter.Increment("NonPostRequest");
                throw new RequestException("Only POST calls are allowed.");
            }

            if (context.Request.ContentType == null || context.Request.ContentType.StartsWith("application/json") == false)
            {
                context.Response.Headers.Add("Accept", "application/json");
                _failureCounter.Increment("NonJsonRequest");
                throw new RequestException("Only requests with content type 'application/json' are supported.");
            }

            if (context.Request.ContentLength64 == 0)
            {
                _failureCounter.Increment("EmptyRequest");
                throw new RequestException("Only requests with content are supported.");
            }


        }


        private async Task CheckSecureConnection(HttpListenerContext context)
        {
            if (!context.Request.IsSecureConnection)
            {
                // If a non-secure request reaches the HTTP port, we are safe
                if (context.Request.LocalEndPoint?.Port == ServiceEndPointDefinition.HttpPort)
                {
                    return;
                }

                _failureCounter.Increment("IncorrectSecurityType");
                throw new SecureRequestException("Incompatible channel security - both client and server must be either secure or insecure.",
                    unencrypted: new Tags { { "requestIsSecure", context.Request.IsSecureConnection.ToString() }, { "requestedUrl", context.Request.Url.ToString() } });
            }

            if (ServiceEndPointDefinition.ClientCertificateVerification == ClientCertificateVerificationMode.Disable)
            {
                return;
            }

            var clientCertificate = await context.Request.GetClientCertificateAsync();

            if (clientCertificate == null)
            {
                _failureCounter.Increment("MissingClientCertificate");
                throw new SecureRequestException("Client certificate is not present.");
            }

            var isValid = clientCertificate.HasSameRootCertificateHash(ServerRootCertHash);

            if (!isValid) // Invalid certificate
            {
                _failureCounter.Increment("InvalidClientCertificate");
                throw new SecureRequestException("Client certificate is not valid.");
            }
        }

        /// <summary>
        /// Writes to response output stream and returns response time
        /// </summary>
        /// <param name="context"></param>
        /// <param name="data"></param>
        /// <param name="httpStatus"></param>
        /// <param name="contentType"></param>
        /// <returns>double? - response time</returns>
        private async Task<bool> TryWriteResponse(HttpListenerContext context, string data, HttpStatusCode httpStatus = HttpStatusCode.OK, string contentType = "application/json", ServiceCallEvent serviceCallEvent = null)
        {
            context.Response.Headers.Add(GigyaHttpHeaders.ProtocolVersion, HttpServiceRequest.ProtocolVersion);

            var body = Encoding.UTF8.GetBytes(data ?? "");

            context.Response.StatusCode = (int)httpStatus;
            context.Response.ContentLength64 = body.Length;
            context.Response.ContentType = contentType;
            context.Response.Headers.Add(GigyaHttpHeaders.DataCenter, Environment.Zone);
            context.Response.Headers.Add(GigyaHttpHeaders.Environment, Environment.DeploymentEnvironment);
            context.Response.Headers.Add(GigyaHttpHeaders.ServiceVersion, AppInfo.Version.ToString());
            context.Response.Headers.Add(GigyaHttpHeaders.ServerHostname, CurrentApplicationInfo.HostName);
            context.Response.Headers.Add(GigyaHttpHeaders.SchemaHash, ServiceSchema.Hash);

            try
            {
                await WriteResponseAndMeasureTime(context, body, serviceCallEvent);
                return true;
            }
            catch (HttpListenerException writeEx)
            {
                if (serviceCallEvent != null)
                    serviceCallEvent.Exception = writeEx;
                // For some reason, HttpListener.IgnoreWriteExceptions doesn't work here.
                Log.Warn(_ => _("HttpServiceListener: Failed to write the response of a service call. See exception and tags for details.",
                    exception: writeEx,
                    unencryptedTags: new
                    {
                        remoteEndpoint = context.Request.RemoteEndPoint,
                        rawUrl = context.Request.RawUrl,
                        status = httpStatus
                    },
                    encryptedTags: new { response = data }));
                return false;
            }
        }

        private async Task WriteResponseAndMeasureTime(HttpListenerContext context, byte[] body, ServiceCallEvent serviceCallEvent = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
            sw.Stop();

            if (serviceCallEvent != null)
                serviceCallEvent.ClientResponseTime = sw.Elapsed.TotalMilliseconds;
        }

        private async Task<HttpServiceRequest> ParseRequest(HttpListenerContext context)
        {
            var request = await _deserializationTime.Time(async () =>
            {
                using (var streamReader = new StreamReader(context.Request.InputStream))
                {
                    var json = await streamReader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<HttpServiceRequest>(json, JsonSettings);
                }
            });

            // TODO: We have an issue when calling from Legacy:
            // http://kibana.gigya.net/kibana3/#/dashboard/elasticsearch/logdog_dashboard?query=callID:8f74364e9cab4aa4954374b8064155a1&from=2019-07-23T10:29:15.218Z&to=2019-07-23T10:31:15.220Z

            // var errors = new List<ValidationResult>();
            // 
            // if (   !_validator.TryValidateObjectRecursive(request.Overrides, errors) 
            //     || !_validator.TryValidateObjectRecursive(request.TracingData, errors)
            //     )
            // {
            //     _failureCounter.Increment("InvalidRequestFormat");
            //     throw new RequestException("Invalid request format: " + string.Join("\n", errors.Select(a => a.MemberNames + ": " + a.ErrorMessage)));
            // }

            request.TracingData = request.TracingData ?? new TracingData();
            request.TracingData.RequestID = request.TracingData.RequestID ?? Guid.NewGuid().ToString("N");

            return request;
        }

        private async Task<string> GetResponse(HttpListenerContext context, ServiceMethod serviceMethod, HttpServiceRequest requestData, object[] arguments)
        {
            var taskType = serviceMethod.ServiceInterfaceMethod.ReturnType;
            var resultType = taskType.IsGenericType ? taskType.GetGenericArguments().First() : null;
            var settings = requestData.Target.IsWeaklyTyped ? JsonSettingsWeak : JsonSettings;

            var invocationResult = await Activator.Invoke(serviceMethod, arguments);
            string response = _serializationTime.Time(() => JsonConvert.SerializeObject(invocationResult.Result, resultType, settings));
            context.Response.Headers.Add(GigyaHttpHeaders.ExecutionTime, invocationResult.ExecutionTime.ToString());

            return response;
        }

        private static object[] GetParametersByName(ServiceMethod serviceMethod, IDictionary args)
        {
            return serviceMethod.ServiceInterfaceMethod
                .GetParameters()
                .Select(p =>
                    {
                        try
                        {
                            return JsonHelper.ConvertWeaklyTypedValue(args[p.Name], p.ParameterType);
                        }
                        catch (InvalidParameterValueException ex)
                        {
                            if (ex.parameterName != null)
                                throw;

                            throw new InvalidParameterValueException(p.Name, ex.ErrorPath, ex.Message, ex);
                        }
                    }).
                    ToArray();
        }

        internal static HttpStatusCode GetExceptionStatusCode(Exception exception)
        {
            if (exception is SecureRequestException)
                return HttpStatusCode.Forbidden;
            if (exception is MissingMethodException)
                return HttpStatusCode.NotFound;
            if (exception is RequestException || exception is JsonException)
                return HttpStatusCode.BadRequest;
            if (exception is EnvironmentException)
                return HttpStatusCode.ServiceUnavailable;

            return HttpStatusCode.InternalServerError;
        }


        public void Dispose()
        {
            Worker.Dispose();
            Listener.Close();
        }

        public void Stop()
        {

        }
    }
}
