using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.ServiceDiscovery
{
    /// <summary>
    /// Provides an up-to-date list of endpoints.
    /// </summary>
    public abstract class ServiceDiscoverySourceBase : IDisposable
    {
        public string DeploymentName { get; }
        public EndPoint[] EndPoints { get; protected set; } = new EndPoint[0];
        public BroadcastBlock<EndPointsResult> EndPointsChanged { get; } = new BroadcastBlock<EndPointsResult>(null);
        public abstract bool IsServiceDeploymentDefined { get; }
        public virtual Task InitCompleted { get; } = Task.FromResult(1);


        protected ServiceDiscoverySourceBase(string deploymentName)
        {
            DeploymentName = deploymentName;
        }


        public abstract Exception AllEndpointsUnreachable(
            EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts);


        public virtual void ShutDown()
        {
            EndPointsChanged?.Complete();
        }


        public void Dispose()
        {
            ShutDown();
        }
    }
}
