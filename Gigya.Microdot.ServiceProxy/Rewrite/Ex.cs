using System;
using System.Net;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
    internal static class Ex
    {
        public static RemoteServiceException BadHttpResponse(string uri, Exception innerException)
        {
            return new RemoteServiceException(
                "The remote service failed to return a valid HTTP response. See tags for URL and exception for details.",
                uri,
                innerException,
                unencrypted: new Tags
                {
                    { "requestUri" , uri }
                });
        }


        public static RemoteServiceException NonMicrodotHost(string uri, HttpStatusCode code)
	    {
	        return new RemoteServiceException(
	            $"The remote service returned HTTP code {(int)code} but is not recognized as a Microdot host.", 
	            uri,
	            unencrypted: new Tags
	            {
	                { "requestUri" , uri }
	            });
	    }

	    public static RemoteServiceException Timeout(string uri, Exception innerException, TimeSpan timeout)
	    {
	        return new RemoteServiceException(
	            "The request to the remote service exceeded the allotted timeout. See the 'RequestUri' property " +
	            "on this exception for the URL that was called and the tag 'requestTimeout' for the configured timeout.",
	            uri, 
	            innerException, 
	            unencrypted: new Tags
	            {
	                { "requestTimeout" , timeout.ToString()},
	                { "requestUri" , uri }
	            });
	    }

	    public static RemoteServiceException UnparsableFailureResponse(string uri, Exception innerException, string responseContent)
	    {
            return new RemoteServiceException(
                "The remote service returned a failure response that failed to deserialize. See the 'RequestUri' property on this " +
                "exception for the URL that was called, the inner exception for the exact error and the 'responseContent' encrypted " +
                "tag for the original response content.",
                uri,
                innerException,
                encrypted: new Tags
                {
                    { "responseContent", responseContent },
                },
                unencrypted: new Tags
                {
                    { "requestUri" , uri }
                });
	    }

	    public static RemoteServiceException FailureResponse(string uri, Exception remoteException)
	    {
	        return new RemoteServiceException(
	            "The remote service returned a failure response. See the 'RequestUri' property on this exception for the URL that " +
	            "was called, and the inner exception for details.",
	            uri,
	            remoteException,
	            unencrypted: new Tags
	            {
	                { "requestUri" , uri }
	            });
	    }

        public static RemoteServiceException UnparsableJsonResponse(string uri, Exception innerException, string responseContent)
        {
            return new RemoteServiceException(
                "The remote service returned a response with JSON that failed deserialization. See the 'RequestUri' property on this " + 
                "exception for the URL that was called, the inner exception for the exact error and the 'responseContent' encrypted " + 
                "tag for the original response content.",
                uri,
                innerException,
                encrypted: new Tags
                {
                    { "responseContent", responseContent }
                },
                unencrypted: new Tags
                {
                    { "requestUri", uri }
                });
        }

        public static ServiceUnreachableException RoutingFailed(string requestedService, string[] environmentCandidates, string[] availableEnvironments)
        {
            return new ServiceUnreachableException(
                "Failed to route request to the correct environment because all routing candidates are unavailable. See tags for " + 
                "the requested service name, the candidate list and the list of all available environments for this service.",
                unencrypted: new Tags
                {
                    { "requestedService", requestedService },
                    { "environmentCandidates", string.Join(", ", environmentCandidates) },
                    { "availableEnvironments", string.Join(",", availableEnvironments) }
                });
        }
    }
}
