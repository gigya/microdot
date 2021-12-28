using System;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public class GCCustomEndpoint:ICustomEndpoint
    {
        private readonly IGCEndpointHandler _gcEndpointHandler;

        public GCCustomEndpoint(IGCEndpointHandler gcEndpointHandler)
        {
            _gcEndpointHandler = gcEndpointHandler;
        }
        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            var url = context?.Request.Url;
            var queryString = context?.Request.QueryString;

            var gcHandleResult =  await _gcEndpointHandler.Handle(url, queryString);

            if (gcHandleResult.Successful)
            {
                await writeResponse(
                    JsonConvert.SerializeObject(new
                    {
                        gcHandleResult.Message,
                        gcHandleResult.GcCollectionResult
                    })
                    ).ConfigureAwait(false);
                
                return true;
            }

            return false;
        }
    }
}