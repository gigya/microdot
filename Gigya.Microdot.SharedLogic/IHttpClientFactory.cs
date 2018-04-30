using System;
using System.Net.Http;

namespace Gigya.Microdot.SharedLogic
{
    public interface IHttpClientFactory
    {
        HttpClient GetClient(bool https, TimeSpan? requestTimeout);
    }

    class FakeClientFactory : IHttpClientFactory
    {
        public HttpClient GetClient(bool https, TimeSpan? requestTimeout)
        {
            var http = new HttpClient();
            
            if (requestTimeout != null)
                http.Timeout = requestTimeout.Value;

            return http;
        }
    }
}
