using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulQueryNodeSource : INodeSource
    {
        private bool _disposed;
        public string ServiceName { get; set; }

        public INodeMonitor NodeMonitor { get; set; }

        public ConsulQueryNodeSource(DeploymentIdentifier deploymentIdentifier, Func<string, INodeMonitor> getNodeMonitor)
        {
            ServiceName = $"{deploymentIdentifier.ServiceName}-{deploymentIdentifier.DeploymentEnvironment}";
            NodeMonitor = getNodeMonitor(ServiceName);
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
            {
                NodeMonitor?.Dispose();
            }

            _disposed = true;
        }
    }
}