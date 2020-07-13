using System;
using System.Collections.Generic;

namespace Metrics.Endpoints
{
    public class MetricsEndpointRequest
    {
        public readonly IDictionary<string, string[]> Headers;

        public MetricsEndpointRequest(IDictionary<string, string[]> headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            this.Headers = headers;
        }
    }
}
