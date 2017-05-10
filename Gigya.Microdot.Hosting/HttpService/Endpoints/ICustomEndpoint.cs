using System.Net;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public interface ICustomEndpoint
    {
        Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse);
    }

    public delegate Task WriteResponseDelegate(string data, HttpStatusCode httpStatus = HttpStatusCode.OK, string contentType = "application/json");

}
