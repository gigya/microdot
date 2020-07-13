using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;

namespace Metrics.Endpoints
{
    public sealed class MetricsEndpointHandler : AbstractMetricsEndpointHandler<HttpListenerContext>
    {
        public MetricsEndpointHandler(IEnumerable<MetricsEndpoint> endpoints) : base(endpoints) { }

        protected override MetricsEndpointRequest CreateRequest(HttpListenerContext requestInfo)
        {
            var headers = GetHeaders(requestInfo.Request.Headers);
            return new MetricsEndpointRequest(headers);
        }

        private IDictionary<string, string[]> GetHeaders(NameValueCollection headers)
        {
            return headers.AllKeys
                .ToDictionary(
                    key => key,
                    key => headers[key].Split(',')
                        .Select(s => s.Trim())
                        .ToArray());
        }
    }
}
