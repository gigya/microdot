using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.ServiceDiscovery;

namespace Gigya.Microdot.Fakes.Discovery
{

    public class LocalhostServiceDiscovery : IServiceDiscovery
    {
        private readonly Task<IEndPointHandle> _source = Task.FromResult<IEndPointHandle>(new LocalhostEndPointHandle());

        public Task<IEndPointHandle> GetNextHost(string affinityToken = null) => _source;
        public Task<IEndPointHandle> GetOrWaitForNextHost(CancellationToken cancellationToken) => _source;
        public ISourceBlock<string> EndPointsChanged => new BroadcastBlock<string>(null);
        public ISourceBlock<ServiceReachabilityStatus> ReachabilityChanged => new BroadcastBlock<ServiceReachabilityStatus>(null);
    }
}
