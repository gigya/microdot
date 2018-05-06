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
        private Func<string, INodeMonitor> GetNodeMonitor { get; }
        private INodeMonitor NodeMonitor { get; set; }
        private string _deploymentIdentifier;

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
            _deploymentIdentifier = deploymentIdentifier.ToString();
        }

        /// <inheritdoc />
        public async Task Init()
        {
            await ConsulServiceListMonitor.Init().ConfigureAwait(false);

            if (ConsulServiceListMonitor.Services.TryGetValue(_deploymentIdentifier, out string deploymentIdentifierMatchCasing))
            {
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

                _wasUndeployed = !(ConsulServiceListMonitor.Services.TryGetValue(_deploymentIdentifier, out string deploymentIdentifierMatchCasing));
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
            {
                NodeMonitor?.Dispose();
                ConsulServiceListMonitor?.Dispose();
            }

            _disposed = true;
        }
    }
}
