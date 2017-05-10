using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class EndPointsResult
    {
        /// <summary>
        /// Result of endpoints which returned by Consul
        /// </summary>
        public EndPoint[] EndPoints { get; set; }

        /// <summary>
        /// Log of Request sent to Consul
        /// </summary>
        public string RequestLog { get; set; }

        /// <summary>
        /// Log of Response from Consul
        /// </summary>
        public string ResponseLog { get; set; }

        public DateTime RequestDateTime { get; set; }

        public Exception Error { get; set; }

        public bool IsQueryDefined { get; set; }
    }



    public interface IConsulClient
    {
        Task<EndPointsResult> GetEndPoints(string serviceName);
        Uri ConsulAddress { get; }
    }



    public class ConsulEndPoint : EndPoint
    {
        public ulong ModifyIndex { get; set; }
    }

}