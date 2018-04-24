using System;
using System.Net;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulResult<TResponse>
    {
        public INode[] Nodes { get; set; }
        public bool IsDeployed { get; set; } = true;
        public Exception Error { get; set; }

        public bool Success => Error == null && IsDeployed;

        public string RequestLog { get; set; }
        public string ResponseContent { get; set; }
        public TResponse Response { get; set; }
        public DateTime ResponseDateTime { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public ulong? ModifyIndex { get; set; }
    }
}