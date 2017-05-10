using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Interfaces.HttpService;

using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy
{
    public interface IServiceProxyProvider<TInterface>
    {
        TInterface Client { get; }
    }


    public interface IServiceProxyProvider
    {
        Task<object> Invoke(HttpServiceRequest request, Type resultReturnType);
        Task<object> Invoke(HttpServiceRequest request, Type resultReturnType, JsonSerializerSettings jsonSettings);
        Task<ServiceSchema> GetSchema();
        ISourceBlock<string> EndPointsChanged { get; }
        ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged { get; }
        int? DefaultPort { get; set; }
        bool UseHttpsDefault { get; set; }
        string ServiceName { get;  }
        Action<HttpServiceRequest> PrepareRequest { get; set; }
        void SetHttpTimeout(TimeSpan timeout);
    }
}