using System;
using System.Net;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulResult<TResponse>
    {
        public bool IsDeployed { get; set; } = true;
        public EnvironmentException Error { get; private set; }
        public bool IsSuccessful => Error == null && IsDeployed;
        public string ConsulAddress { get; set; }
        public string CommandPath { get; set; }
        public string ResponseContent { get; set; }
        public TResponse Response { get; set; }
        public DateTime ResponseDateTime { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public ulong? ModifyIndex { get; set; }

        public void ConsulUnreachable(Exception innerException)
        {
            Error = new EnvironmentException("Consul was unreachable. See tags and inner exception for details.",
                innerException,
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                });
        }

        public void ConsulResponseError()
        {
            Error = new EnvironmentException("Consul returned a failure response (not 200 OK). See tags for details.",
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                    { "responseContent", ResponseContent },
                    { "responseCode", StatusCode?.ToString() }
                });
        }

        public void UnparsableConsulResponse(Exception innerException)
        {
            Error = new EnvironmentException("Error deserializing Consul response. See tags for details.",
                innerException,
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                    { "responseContent", ResponseContent },
                    { "expectedResponseType", typeof(TResponse).Name }
                });
        }
    }
}