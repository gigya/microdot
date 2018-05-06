using System;
using System.Net.Http;

namespace Gigya.Microdot.SharedLogic.HttpService
{
    class SimpleHttpClientFactory : IHttpClientFactory
    {
        private object SyncLock { get; set; }
        private TimeSpan? RequestTimeout { get; set; }
        private HttpClient Client { get; set; }

        public HttpClient GetClient(bool https, TimeSpan? requestTimeout)
        {
            if (https)
                throw new ArgumentException("SimpleHttpClientFactory is a simpleton and doesn't know enough math to do encryption.");

            lock (SyncLock)
            {
                if (Client != null)
                {
                    if (requestTimeout != RequestTimeout)
                        throw new ArgumentException("SimpleHttpClientFactory is a simpleton and is confused when his parameters are different from the first request.");

                    return Client;
                }

                Client = new HttpClient();
            
                if (requestTimeout != null)
                    Client.Timeout = requestTimeout.Value;

                return Client;
            }
        }
    }
}