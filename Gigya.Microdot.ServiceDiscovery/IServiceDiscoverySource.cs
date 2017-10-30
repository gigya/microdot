using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.ServiceDiscovery
{
    public interface IServiceDiscoverySource : IDisposable
    {
        string SourceName { get; }
        bool SupportsFallback { get; }
        Task Init();

        string Deployment { get; }
        ISourceBlock<EndPointsResult> EndPointsChanged { get; }
        EndPointsResult Result { get; }
        bool IsServiceDeploymentDefined { get; }

        Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException,
            string lastExceptionEndPoint, string unreachableHosts);
    }
}