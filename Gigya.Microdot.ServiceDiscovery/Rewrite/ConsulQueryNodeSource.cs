using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulQueryNodeSource : INodeSource
    {
        private DeploymentIdentifier DeploymentIdentifier { get; }
        private Func<DeploymentIdentifier, INodeMonitor> GetNodeMonitor { get; }
        private bool _disposed;
        private object _initLocker = new object();
        private INodeMonitor NodeMonitor { get; set; }

        public ConsulQueryNodeSource(DeploymentIdentifier deploymentIdentifier, Func<DeploymentIdentifier, INodeMonitor> getNodeMonitor)
        {
            DeploymentIdentifier = deploymentIdentifier;
            GetNodeMonitor = getNodeMonitor;
        }

        public Task Init()
        {
            lock (_initLocker)
            {
                if (NodeMonitor == null)
                    NodeMonitor = GetNodeMonitor(DeploymentIdentifier);
            }
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

            if (NodeMonitor != null)
                await NodeMonitor.DisposeAsync().ConfigureAwait(false);

            _disposed = true;
        }
    }
}