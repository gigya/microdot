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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Measurement;
using Gigya.Microdot.SharedLogic.Security;
using Metrics;
using Newtonsoft.Json;


// ReSharper disable ConsiderUsingConfigureAwait

namespace Gigya.Microdot.Hosting.HttpService
{
    public sealed class HttpServiceListener : IDisposable
    {
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

        private string Prefix { get; }
        private byte[] ServerRootCertHash { get; }

        private IActivator Activator { get; }
        private IWorker Worker { get; }
        private IServiceEndPointDefinition ServiceEndPointDefinition { get; }
        private HttpListener Listener { get; }
        private ILog Log { get; }
        private IEventPublisher<ServiceCallEvent> EventPublisher { get; }
        private IEnumerable<ICustomEndpoint> CustomEndpoints { get; }
        private IEnvironmentVariableProvider EnvironmentVariableProvider { get; }
        private JsonExceptionSerializer ExceptionSerializer { get; }

        private readonly Timer _serializationTime;
        private readonly Timer _deserializationTime;
        private readonly Timer _roundtripTime;
        private readonly Counter _successCounter;
        private readonly Counter _failureCounter;
        private readonly Timer _activeRequestsCounter;
        private readonly Timer _metaEndpointsRoundtripTime;
        private readonly MetricsContext _endpointContext;

        public HttpServiceListener(IActivator activator, IWorker worker, IServiceEndPointDefinition serviceEndPointDefinition,
                                   ICertificateLocator certificateLocator, ILog log, IEventPublisher<ServiceCallEvent> eventPublisher,
                                   IEnumerable<ICustomEndpoint> customEndpoints, IEnvironmentVariableProvider environmentVariableProvider,
                                   JsonExceptionSerializer exceptionSerializer)
        {
            ServiceEndPointDefinition = serviceEndPointDefinition;
            Worker = worker;
            Activator = activator;
            Log = log;
            EventPublisher = eventPublisher;
            CustomEndpoints = customEndpoints.ToArray();
            EnvironmentVariableProvider = environmentVariableProvider;
            ExceptionSerializer = exceptionSerializer;

            if (serviceEndPointDefinition.UseSecureChannel)
                ServerRootCertHash = certificateLocator.GetCertificate("Service").GetHashOfRootCertificate();

            var urlPrefixTemplate = ServiceEndPointDefinition.UseSecureChannel ? "https://+:{0}/" : "http://+:{0}/";
            Prefix = string.Format(urlPrefixTemplate, ServiceEndPointDefinition.HttpPort);

            Listener = new HttpListener
            {
                IgnoreWriteExceptions = true,
                Prefixes = { Prefix }
            };

            var context = Metric.Context("Service").Context(CurrentApplicationInfo.Name);
            _serializationTime = context.Timer("Serialization", Unit.Calls);
            _deserializationTime = context.Timer("Deserialization", Unit.Calls);
            _roundtripTime = context.Timer("Roundtrip", Unit.Calls);
            _metaEndpointsRoundtripTime = context.Timer("MetaRoundtrip", Unit.Calls);
            _successCounter = context.Counter("Success", Unit.Calls);
            _failureCounter = context.Counter("Failed", Unit.Calls);
            _activeRequestsCounter = context.Timer("ActiveRequests", Unit.Requests);
            _endpointContext = context.Context("Endpoints");
        }


        public void Start()
        {
            try
            {
                Listener.Start();
                Log.Info(_ => _("HttpServiceListener started", unencryptedTags: new { prefix = Prefix }));
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode != 5)
                    throw;

                throw new Exception(
                    "One or more of the specified HTTP listen ports wasn't configured to run without administrative permissions.\n" +
                    "To configure them, run the following commands in an elevated (administrator) command prompt:\n" +
                    $"netsh http add urlacl url={Prefix} user={CurrentApplicationInfo.OsUser}");
            }

            StartListening();
        }


        private async void StartListening()
        {
            while (Listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await Listener.GetContextAsync();
                    Worker.FireAndForget(() => HandleRequest(context));
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


        private async Task HandleRequest(HttpListenerContext context)
        {
            RequestTimings.ClearCurrentTimings();
            using (context.Response)            
            {
                var sw = Stopwatch.StartNew();

                // Special endpoints should not be logged/measured/traced like regular endpoints
                try
                {
                    foreach (var customEndpoint in CustomEndpoints)
                    {
                        if (await customEndpoint.TryHandle(context, (data, status, type) => TryWriteResponse(context, data, status, type)))
                        {
                            if (RequestTimings.Current.Request.ElapsedMS != null)
                                _metaEndpointsRoundtripTime.Record((long)RequestTimings.Current.Request.ElapsedMS, TimeUnit.Milliseconds);
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    var ex = GetRelevantException(e);
                    await TryWriteResponse(context, ExceptionSerializer.Serialize(ex), GetExceptionStatusCode(ex));
                    return;
                }

                // Regular endpoint handling
                using (_activeRequestsCounter.NewContext("Request"))
                {
                    TracingContext.SetUpStorage();
                    RequestTimings.GetOrCreate(); // initialize request timing context

                    Exception ex;
                    Exception actualException = null;
                    string methodName = null;
                    // Initialize with empty object for protocol backwards-compatibility.

                    var requestData = new HttpServiceRequest { TracingData = new TracingData() };
                    
                    ServiceMethod serviceMethod = null;
                    try
                    {
                        try
                        {
                            ValidateRequest(context);
                            await CheckSecureConnection(context);

                            requestData = await ParseRequest(context);

                            TracingContext.SetOverrides(requestData.Overrides);

                            serviceMethod = ServiceEndPointDefinition.Resolve(requestData.Target);
                            methodName = serviceMethod.ServiceInterfaceMethod.Name;
                        }
                        catch (Exception e)
                        {
                            actualException = e;
                            if (e is RequestException)
                                throw;

                            throw new RequestException("Invalid request", e);
                        }

                        var responseJson = await GetResponse(context, serviceMethod, requestData);
                        await TryWriteResponse(context, responseJson);

                        _successCounter.Increment();
                    }
                    catch (Exception e)
                    {
                        actualException = actualException ?? e;
                        _failureCounter.Increment();
                        ex = GetRelevantException(e);

                        string json = _serializationTime.Time(() => ExceptionSerializer.Serialize(ex));
                        await TryWriteResponse(context, json, GetExceptionStatusCode(ex));
                    }
                    finally
                    {
                        sw.Stop();
                        _roundtripTime.Record((long)(sw.Elapsed.TotalMilliseconds * 1000000), TimeUnit.Nanoseconds);
                        if (methodName != null)
                            _endpointContext.Timer(methodName, Unit.Requests).Record((long)(sw.Elapsed.TotalMilliseconds * 1000000), TimeUnit.Nanoseconds);
                        PublishEvent(requestData, actualException, serviceMethod, sw.Elapsed.TotalMilliseconds);
                    }
                }
            }
        }


        private static Exception GetRelevantException(Exception e)
        {
            if (e is RequestException)
                return e;

            var ex = GetAllExceptions(e).FirstOrDefault(x => (x is TargetInvocationException || x is AggregateException) == false);
            if (ex is SerializableException == false)
                ex = new UnhandledException(ex);

            return ex;
        }

        private static IEnumerable<Exception> GetAllExceptions( Exception ex)
        {
            while (ex != null)
            {
                yield return ex;
                ex = ex.InnerException;
            }
        }

        private void ValidateRequest(HttpListenerContext context)
        {
            var clientVersion = context.Request.Headers[GigyaHttpHeaders.Version];

            if (clientVersion != null && clientVersion != HttpServiceRequest.Version)
            {
                _failureCounter.Increment("ProtocolVersionMismatch");
                throw new RequestException($"Client protocol version {clientVersion} is not supported by the server protocol version {HttpServiceRequest.Version}.");
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
            if (context.Request.IsSecureConnection != ServiceEndPointDefinition.UseSecureChannel)
            {
                _failureCounter.Increment("IncorrectSecurityType");
                throw new SecureRequestException("Incompatible channel security - both client and server must be either secure or insecure.", unencrypted: new Tags { { "serviceIsSecure", ServiceEndPointDefinition.UseSecureChannel.ToString() }, { "requestIsSecure", context.Request.IsSecureConnection.ToString() }, { "requestedUrl", context.Request.Url.ToString() } });
            }

            if (!context.Request.IsSecureConnection)
                return;

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


        private void PublishEvent(HttpServiceRequest requestData, Exception ex, ServiceMethod serviceMethod, double requestTime)
        {
            var callEvent = EventPublisher.CreateEvent();

            callEvent.CalledServiceName = serviceMethod?.GrainInterfaceType.Name;
            callEvent.ClientMetadata = requestData.TracingData;
            callEvent.ServiceMethod = requestData.Target?.MethodName;

            var metaData = ServiceEndPointDefinition.GetMetaData(serviceMethod);

            callEvent.Params = (requestData.Arguments ?? new OrderedDictionary()).Cast<DictionaryEntry>().Select(arg => new Param
            {
                Name = arg.Key.ToString(),
                Value = arg.Value is string ? arg.Value.ToString() : JsonConvert.SerializeObject(arg.Value),
                Sensitivity = metaData.ParametersSensitivity[arg.Key.ToString()] ?? metaData.MethodSensitivity ?? Sensitivity.Sensitive
            });

            callEvent.Exception = ex;
            callEvent.ActualTotalTime = requestTime;
            callEvent.ErrCode = ex != null ? null : (int?)0;

            EventPublisher.TryPublish(callEvent); // fire and forget!
        }


        private async Task TryWriteResponse(HttpListenerContext context, string data, HttpStatusCode httpStatus = HttpStatusCode.OK, string contentType = "application/json")
        {
            context.Response.Headers.Add(GigyaHttpHeaders.Version, HttpServiceRequest.Version);

            var body = Encoding.UTF8.GetBytes(data ?? "");

            context.Response.StatusCode = (int)httpStatus;
            context.Response.ContentLength64 = body.Length;
            context.Response.ContentType = contentType;
            context.Response.Headers.Add(GigyaHttpHeaders.DataCenter, EnvironmentVariableProvider.DataCenter);
            context.Response.Headers.Add(GigyaHttpHeaders.Environment, EnvironmentVariableProvider.DeploymentEnvironment);
            context.Response.Headers.Add(GigyaHttpHeaders.ServiceVersion, CurrentApplicationInfo.Version.ToString());
            context.Response.Headers.Add(GigyaHttpHeaders.ServerHostname, CurrentApplicationInfo.HostName);
            try
            {
                await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
            }
            catch (HttpListenerException writeEx)
            {
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
            }
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

            request.TracingData = request.TracingData ?? new TracingData();
            request.TracingData.RequestID = request.TracingData.RequestID ?? Guid.NewGuid().ToString("N");

            TracingContext.SetRequestID(request.TracingData.RequestID);
            TracingContext.SetSpan(request.TracingData.SpanID, request.TracingData.ParentSpanID);            
            return request;
        }


        private async Task<string> GetResponse(HttpListenerContext context, ServiceMethod serviceMethod, HttpServiceRequest requestData)
        {
            var taskType = serviceMethod.ServiceInterfaceMethod.ReturnType;
            var resultType = taskType.IsGenericType ? taskType.GetGenericArguments().First() : null;
            var arguments = requestData.Target.IsWeaklyTyped ? GetParametersByName(serviceMethod, requestData.Arguments) : requestData.Arguments.Values.Cast<object>().ToArray();
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
                .Select(p => args[p.Name] ?? (p.ParameterType.IsValueType ? System.Activator.CreateInstance(p.ParameterType) : null))
                .ToArray();
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
    }
}
