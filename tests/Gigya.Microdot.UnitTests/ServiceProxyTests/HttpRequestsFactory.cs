using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Exceptions;
using System;
using System.Net;
using System.Net.Http;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    public static class HttpResponseFactory
    {
       public static HttpResponseMessage GetResponseWithException(JsonExceptionSerializer exceptionSerializer, Exception ex, HttpStatusCode? statusCode = null, bool withGigyaHostHeader = true) 
       {
           var resMessage = new HttpResponseMessage { StatusCode = statusCode ?? HttpServiceListener.GetExceptionStatusCode(ex) };
           
           if (withGigyaHostHeader)
           {
               resMessage.Headers.Add(GigyaHttpHeaders.ServerHostname, "host");
           }

           resMessage.Content = new StringContent(exceptionSerializer.Serialize(ex));

           return resMessage; 
       }

       public static HttpResponseMessage GetResponse(HttpStatusCode statusCode = HttpStatusCode.OK, bool isGigyaHost = true,string content="")
       {
           var resMessage = new HttpResponseMessage { StatusCode = statusCode, Content = new StringContent(content) };
           if (isGigyaHost)
           {
               resMessage.Headers.Add(GigyaHttpHeaders.ServerHostname, "host");
               resMessage.Headers.Add(GigyaHttpHeaders.ExecutionTime,"00:00:15");
           }
           return resMessage;
       }
    }
}
