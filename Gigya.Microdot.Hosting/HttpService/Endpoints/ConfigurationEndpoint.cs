using System.Net;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class ConfigurationEndpoint : ICustomEndpoint
    {
        private readonly ConfigurationResponseBuilder _responseBuilder;


        public ConfigurationEndpoint(ConfigurationResponseBuilder responseBuilder)
        {
            _responseBuilder = responseBuilder;
        }


        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            if (context.Request.Url.AbsolutePath == "/config")
            {
                var format = context.Request.QueryString["format"] ?? "text";
                switch (format)
                {
                    case "json":
                        var json = _responseBuilder.BuildJson();
                        await writeResponse(json);
                        break;
                    default:
                        var text = _responseBuilder.BuildText();
                        await writeResponse(text, contentType: "text/plain");
                        break;
                }

                return true;
            }

            return false;
        }
    }
}
