using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Gigya.Microdot.ServiceContract.HttpService;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{

    public class SchemaEndpoint : ICustomEndpoint
    {
        private readonly string _jsonSchema;

        public SchemaEndpoint(IServiceInterfaceMapper mapper)
        {
            _jsonSchema = JsonConvert.SerializeObject(new ServiceSchema(mapper.ServiceInterfaceTypes.ToArray()), Formatting.Indented);
        }


        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            if (context.Request.Url.AbsolutePath.EndsWith("/schema"))
            {
                await writeResponse(_jsonSchema);
                return true;
            }

            return false;
        }
    }
}
