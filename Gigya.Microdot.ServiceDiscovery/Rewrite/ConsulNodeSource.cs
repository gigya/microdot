using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulNodeSource: INodeSource
    {
        /// <inheritdoc />
        public virtual string Type => "Consul";

        public bool SupportsMultipleEnvironments => true;

        private DeploymentIdentifier DeploymentIdentifier { get; }
        private IConsulServiceListMonitor ConsulServiceListMonitor { get; }
        private Func<string, INodeMonitor> GetNodeMonitor { get; }
        private INodeMonitor NodeMonitor { get; set; }             
        private string ServiceName { get; set; }

        private bool _disposed;
        private INode[] _lastKnownNodes;
        private bool _isActive;        
        private bool _wasUndeployed;

        public ConsulNodeSource(DeploymentIdentifier deploymentIdentifier,
            IConsulServiceListMonitor consulServiceListMonitor,
            Func<string, INodeMonitor> getNodeMonitor)
        {
            ConsulServiceListMonitor = consulServiceListMonitor;
            GetNodeMonitor = getNodeMonitor;
            ServiceName = deploymentIdentifier.ToString();

            DeploymentIdentifier = deploymentIdentifier;
        }

        /// <inheritdoc />
        public async Task Init()
        {
            await ConsulServiceListMonitor.Init().ConfigureAwait(false);

            if (ConsulServiceListMonitor.Services.Contains(ServiceName))
            {
                var serviceNameMatchCasing = ConsulServiceListMonitor.Services.FirstOrDefault(s => s.Equals(ServiceName, StringComparison.InvariantCultureIgnoreCase));
                if (serviceNameMatchCasing != null)
                {
                    ServiceName = serviceNameMatchCasing;
                    NodeMonitor = GetNodeMonitor(ServiceName);
                    await NodeMonitor.Init().ConfigureAwait(false);
                }
            }
            else
                _wasUndeployed = true;
        }

        /// <inheritdoc />
        public INode[] GetNodes() => NodeMonitor?.Nodes ?? new INode[0];

        /// <inheritdoc />
        public bool WasUndeployed
        {
            get
            {
                if (_wasUndeployed)
                    return true;

                _wasUndeployed = NodeMonitor.WasUndeployed;
                return _wasUndeployed;
            }
        } 

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
                ConsulServiceListMonitor?.Dispose();
            }

            _disposed = true;
        }
    }
}
