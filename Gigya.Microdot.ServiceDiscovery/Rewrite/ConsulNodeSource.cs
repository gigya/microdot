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

        private IConsulServiceListMonitor ConsulServiceListMonitor { get; }
        private Func<DeploymentIdentifier, INodeMonitor> GetNodeMonitor { get; }
        private INodeMonitor NodeMonitor { get; set; }
        private DeploymentIdentifier _deploymentIdentifier;

        private bool _disposed;
        private INode[] _lastKnownNodes;
        private bool _isActive;        
        private bool _wasUndeployed;

        public ConsulNodeSource(DeploymentIdentifier deploymentIdentifier,
            IConsulServiceListMonitor consulServiceListMonitor,
            Func<DeploymentIdentifier, INodeMonitor> getNodeMonitor)
        {
            ConsulServiceListMonitor = consulServiceListMonitor;
            GetNodeMonitor = getNodeMonitor;
            _deploymentIdentifier = deploymentIdentifier;
        }

        /// <inheritdoc />
        public async Task Init()
        {
            await ConsulServiceListMonitor.Init().ConfigureAwait(false);

            if (ConsulServiceListMonitor.ServiceExists(_deploymentIdentifier, out var deploymentIdentifierMatchCasing))
            {
                // TODO: Remove if consul is guaranteed to be with correct casing
                _deploymentIdentifier = deploymentIdentifierMatchCasing;
                NodeMonitor = GetNodeMonitor(_deploymentIdentifier);
                await NodeMonitor.Init().ConfigureAwait(false);
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

                // we do this to get the correct casing of the service name. TODO: can we rely on consul to use correct casings and remove the dependency on the services list
                // and use some narrower API such as IsServiceExists(string serviceName) instead?
                _wasUndeployed = !(ConsulServiceListMonitor.ServiceExists(_deploymentIdentifier, out var deploymentIdentifierMatchCasing));
                // TODO: Remove if consul is guaranteed to be with correct casing
                if (deploymentIdentifierMatchCasing != _deploymentIdentifier)
                    _wasUndeployed = true;

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
