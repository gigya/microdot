using System;
using System.Net.Http;

namespace Gigya.Microdot.SharedLogic.HttpService
{
    public interface IHttpClientFactory
    {
        HttpClient GetClient(bool https, TimeSpan? requestTimeout);
    }
}
