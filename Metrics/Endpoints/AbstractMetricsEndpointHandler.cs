using System.Collections.Generic;
using System.Linq;

namespace Metrics.Endpoints
{
    public abstract class AbstractMetricsEndpointHandler<T>
    {
        private readonly MetricsEndpoint[] endpoints;

        protected AbstractMetricsEndpointHandler(IEnumerable<MetricsEndpoint> endpoints)
        {
            this.endpoints = endpoints.ToArray();
        }

        public MetricsEndpointResponse Process(string urlPath, T requestInfo)
        {
            if (string.IsNullOrEmpty(urlPath))
            {
                return null;
            }

            foreach (var endpoint in this.endpoints)
            {
                if (endpoint.IsMatch(urlPath))
                {
                    var request = CreateRequest(requestInfo);
                    return endpoint.ProduceResponse(request);
                }
            }

            return null;
        }

        protected abstract MetricsEndpointRequest CreateRequest(T requestInfo);
    }
}
