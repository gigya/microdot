using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Metrics.Logging;

namespace Metrics.Endpoints
{
    public sealed class MetricsHttpListener : IDisposable
    {
        private static readonly ILog log = LogProvider.GetCurrentClassLogger();

        private const string NotFoundResponse = "<!doctype html><html><body>Resource not found</body></html>";
        private readonly HttpListener httpListener;
        private readonly CancellationTokenSource cts;
        private readonly string prefixPath;
        private readonly MetricsEndpointHandler endpointHandler;

        private Task processingTask;

        private static readonly Timer timer = Metric.Internal.Context("HTTP").Timer("Request", Unit.Requests);
        private static readonly Meter errors = Metric.Internal.Context("HTTP").Meter("Request Errors", Unit.Errors);

        private MetricsHttpListener(string listenerUriPrefix, IEnumerable<MetricsEndpoint> endpoints, CancellationToken token)
        {
            this.cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            this.prefixPath = ParsePrefixPath(listenerUriPrefix);
            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(listenerUriPrefix);
            this.endpointHandler = new MetricsEndpointHandler(endpoints);
        }

        public static Task<MetricsHttpListener> StartHttpListenerAsync(string httpUriPrefix, IEnumerable<MetricsEndpoint> endpoints, CancellationToken token, int maxRetries = 1)
        {
            return Task.Factory.StartNew(async () =>
            {
                MetricsHttpListener listener = null;
                var remainingRetries = maxRetries;
                do
                {
                    try
                    {
                        listener = new MetricsHttpListener(httpUriPrefix, endpoints, token);
                        listener.Start();
                        if (remainingRetries != maxRetries)
                        {
                            log.InfoFormat("HttpListener started successfully after {0} retries", maxRetries - remainingRetries);
                        }
                        remainingRetries = 0;
                    }
                    catch (Exception x)
                    {
                        using (listener) { }
                        listener = null;
                        remainingRetries--;
                        if (remainingRetries > 0)
                        {
                            log.WarnException("Unable to start HTTP Listener. Sleeping for {0} sec and retrying {1} more times", x, maxRetries - remainingRetries, remainingRetries);
                            await Task.Delay(1000 * (maxRetries - remainingRetries), token).ConfigureAwait(false);
                        }
                        else
                        {
                            MetricsErrorHandler.Handle(x, $"Unable to start HTTP Listener. Retried {maxRetries} times, giving up...");
                        }
                    }
                } while (remainingRetries > 0);
                return listener;
            }, token).Unwrap();
        }

        private static string ParsePrefixPath(string listenerUriPrefix)
        {
            var match = Regex.Match(listenerUriPrefix, @"http://(?:[^/]*)(?:\:\d+)?/(.*)");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
        }

        public void Start()
        {
            this.httpListener.Start();
            this.processingTask = Task.Factory.StartNew(ProcessRequests, this.cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ProcessRequests()
        {
            while (!this.cts.IsCancellationRequested)
            {
                try
                {
                    var context = this.httpListener.GetContext();
                    try
                    {
                        using (timer.NewContext())
                        {
                            ProcessRequest(context);
                            using (context.Response.OutputStream) { }
                            using (context.Response) { }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Mark();
                        context.Response.StatusCode = 500;
                        context.Response.StatusDescription = "Internal Server Error";
                        context.Response.Close();
                        MetricsErrorHandler.Handle(ex, "Error processing HTTP request");
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    if ((ex.ObjectName == this.httpListener.GetType().FullName) && (this.httpListener.IsListening == false))
                    {
                        return; // listener is closed/disposed
                    }
                    MetricsErrorHandler.Handle(ex, "Error processing HTTP request");
                }
                catch (Exception ex)
                {
                    errors.Mark();
                    var httpException = ex as HttpListenerException;
                    if (httpException == null || httpException.ErrorCode != 995)// IO operation aborted
                    {
                        MetricsErrorHandler.Handle(ex, "Error processing HTTP request");
                    }
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            if (context.Request.HttpMethod.ToUpperInvariant() != "GET")
            {
                WriteNotFound(context);
                return;
            }

            var urlPath = context.Request.RawUrl.Substring(this.prefixPath.Length)
                .ToLowerInvariant();
            try
            {
                if (TryProcessFixedEndpoints(context, urlPath))
                {
                    return;
                }

                if (TryProcessDynamicEndpoints(context, urlPath))
                {
                    return;
                }
            }
            catch (HttpListenerException ex)
            {
                // bug/141833
                log.InfoException($"DebugMetrics - Failed to process request {urlPath}", ex);
                throw;
            }

            WriteNotFound(context);
        }

        private static bool TryProcessFixedEndpoints(HttpListenerContext context, string urlPath)
        {
            switch (urlPath)
            {
                case "/":
                    if (!context.Request.Url.ToString().EndsWith("/"))
                    {
                        context.Response.Redirect(context.Request.Url + "/");
                    }
                    else
                    {
                        WriteFlotApp(context);
                    }
                    break;
                case "/favicon.ico":
                    WriteFavIcon(context);
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool TryProcessDynamicEndpoints(HttpListenerContext context, string urlPath)
        {
            var response = this.endpointHandler.Process(urlPath, context);
            if (response != null)
            {
                WriteEndpointResponse(response, context);
                return true;
            }

            return false;
        }

        private static void WriteEndpointResponse(MetricsEndpointResponse response, HttpListenerContext context)
        {
            WriteString(context, response.Content, response.ContentType, response.StatusCode, response.StatusCodeDescription, response.Encoding);
        }

        private static void WriteNotFound(HttpListenerContext context)
        {
            WriteString(context, NotFoundResponse, "text/html", 404, "NOT FOUND");
        }

        private static void WriteString(HttpListenerContext context, string data, string contentType,
            int httpStatus = 200, string httpStatusDescription = "OK", Encoding encoding = null)
        {
            AddCorsHeaders(context.Response);
            AddNoCacheHeaders(context.Response);

            context.Response.ContentType = contentType;
            context.Response.StatusCode = httpStatus;
            context.Response.StatusDescription = httpStatusDescription;

            var textEncoding = encoding ?? Encoding.UTF8;

            var acceptsGzip = AcceptsGzip(context.Request);
            if (!acceptsGzip)
            {
                using (var writer = new StreamWriter(context.Response.OutputStream, textEncoding, 4096, true))
                {
                    writer.Write(data);
                }
            }
            else
            {
                context.Response.AddHeader("Content-Encoding", "gzip");
                using (var gzip = new GZipStream(context.Response.OutputStream, CompressionMode.Compress, true))
                using (var writer = new StreamWriter(gzip, textEncoding, 4096, true))
                {
                    writer.Write(data);
                }
            }
        }

        private static void WriteFavIcon(HttpListenerContext context)
        {
            context.Response.ContentType = FlotWebApp.FavIconMimeType;
            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "OK";

            FlotWebApp.WriteFavIcon(context.Response.OutputStream);
        }

        private static void WriteFlotApp(HttpListenerContext context)
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "OK";

            var acceptsGzip = AcceptsGzip(context.Request);

            if (acceptsGzip)
            {
                context.Response.AddHeader("Content-Encoding", "gzip");
            }

            FlotWebApp.WriteFlotAppAsync(context.Response.OutputStream, !acceptsGzip);
        }

        private static bool AcceptsGzip(HttpListenerRequest request)
        {
            var encoding = request.Headers["Accept-Encoding"];
            return !string.IsNullOrEmpty(encoding) && encoding.Contains("gzip");
        }

        private static void AddNoCacheHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            response.Headers.Add("Pragma", "no-cache");
            response.Headers.Add("Expires", "0");
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
        }

        private void Stop()
        {
            if (!this.cts.IsCancellationRequested)
            {
                this.cts.Cancel();
            }
            if (this.processingTask != null && !this.processingTask.IsCompleted)
            {
                this.processingTask.Wait(1000);
            }
            if (this.httpListener.IsListening)
            {
                this.httpListener.Stop();
                this.httpListener.Prefixes.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
            this.httpListener.Close();
            using (this.cts) { }
            using (this.httpListener) { }
        }
    }
}
