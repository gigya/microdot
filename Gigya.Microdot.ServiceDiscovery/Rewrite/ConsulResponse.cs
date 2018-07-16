using System;
using System.Net;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulResponse<TResult>
    {
        public bool? IsUndeployed { get; set; }
        public EnvironmentException Error { get; private set; }
        public string ConsulAddress { get; set; }
        public string CommandPath { get; set; }
        public string ResponseContent { get; set; }
        public TResult Result { get; set; }
        public DateTime ResponseDateTime { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public ulong? ModifyIndex { get; set; }

        public ConsulResponse<T> SetResult<T>(T result)
        {
            return new ConsulResponse<T>
            {
                IsUndeployed = IsUndeployed,
                Error = Error,
                CommandPath = CommandPath,
                ConsulAddress = ConsulAddress,
                ModifyIndex = ModifyIndex,
                ResponseContent = ResponseContent,
                ResponseDateTime = ResponseDateTime,
                StatusCode = StatusCode,
                Result = result
            };
        }

        public void ConsulUnreachable(Exception innerException)
        {
            Error = new EnvironmentException("Consul was unreachable.",
                innerException,
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                });
        }

        public void ConsulResponseError()
        {
            Error = new EnvironmentException("Consul returned a failure response (not 200 OK).",
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
            Error = new EnvironmentException("Error deserializing Consul response.",
                innerException,
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                    { "responseContent", ResponseContent },
                    { "expectedResponseType", typeof(TResult).Name }
                });
        }
    }
}