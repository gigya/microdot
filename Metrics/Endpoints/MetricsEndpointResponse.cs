using System;
using System.Text;

namespace Metrics.Endpoints
{
    public sealed class MetricsEndpointResponse
    {
        public readonly string Content;
        public readonly string ContentType;
        public readonly Encoding Encoding;
        public readonly int StatusCode;
        public readonly string StatusCodeDescription;

        public MetricsEndpointResponse(string content, string contentType)
            : this(content, contentType, Encoding.UTF8) { }

        public MetricsEndpointResponse(string content, string contentType, Encoding encoding, int statusCode = 200, string statusCodeDescription = "OK")
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new ArgumentException("Invalid content type");
            }
            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }
            if (statusCodeDescription == null)
            {
                throw new ArgumentNullException(nameof(statusCodeDescription));
            }

            this.Content = content;
            this.ContentType = contentType;
            this.Encoding = encoding;
            this.StatusCode = statusCode;
            this.StatusCodeDescription = statusCodeDescription;
        }
    }
}
