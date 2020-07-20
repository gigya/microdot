using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Metrics.Json;

namespace Metrics.RemoteMetrics
{
    public static class HttpRemoteMetrics
    {
        private class CustomClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                return request;
            }
        }

        public static async Task<JsonMetricsContext> FetchRemoteMetrics(Uri remoteUri, Func<string, JsonMetricsContext> deserializer, CancellationToken token)
        {
            using (CustomClient client = new CustomClient())
            {
                client.Headers.Add("Accept-Encoding", "gzip");
                var json = await client.DownloadStringTaskAsync(remoteUri).ConfigureAwait(false);
                return deserializer(json);
            }
        }
    }
}
