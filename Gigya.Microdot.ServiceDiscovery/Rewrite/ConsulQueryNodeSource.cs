using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulQueryNodeSource : INodeSource
    {
        private bool _disposed;
        public INodeMonitor NodeMonitor { get; set; }

        public ConsulQueryNodeSource(DeploymentIdentifier deploymentIdentifier, Func<DeploymentIdentifier, INodeMonitor> getNodeMonitor)
        {            
            NodeMonitor = getNodeMonitor(deploymentIdentifier);
        }

        public Task Init()
        {
            return NodeMonitor.Init();
        }

        public string Type => "ConsulQuery";

        public INode[] GetNodes() => NodeMonitor.Nodes;

        public bool WasUndeployed => NodeMonitor.WasUndeployed;

        public bool SupportsMultipleEnvironments => true;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
                DisposeAsync().Wait(TimeSpan.FromSeconds(3));

            _disposed = true;
        }

        public async Task DisposeAsync()
        {
            if (_disposed)
                return;

            if (NodeMonitor!=null)
                await NodeMonitor.DisposeAsync().ConfigureAwait(false);

            _disposed = true;
        }
    }
}