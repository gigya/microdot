using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulNodeSource: INodeSource
    {
        public virtual string Type => "Consul";

        public bool SupportsMultipleEnvironments => true;

        private DeploymentIdentifier DeploymentIdentifier { get; }
        private IServiceListMonitor ServiceListMonitor { get; }
        private Func<string, INodeMonitor> GetNodeMonitor { get; }
        private INodeMonitor NodeMonitor { get; set; }             
        public string ServiceName { get; private set; }

        private bool _disposed;
        private INode[] _lastKnownNodes;
        private bool _isActive;        
        private bool _wasUndeployed;

        public ConsulNodeSource(DeploymentIdentifier deploymentIdentifier,
            IServiceListMonitor serviceListMonitor,
            Func<string, INodeMonitor> getNodeMonitor)
        {
            ServiceListMonitor = serviceListMonitor;
            GetNodeMonitor = getNodeMonitor;
            ServiceName = $"{deploymentIdentifier.ServiceName}-{deploymentIdentifier.DeploymentEnvironment}";

            DeploymentIdentifier = deploymentIdentifier;
        }

        public async Task Init()
        {
            await ServiceListMonitor.Init().ConfigureAwait(false);

            if (ServiceListMonitor.Services.Contains(ServiceName))
            {
                var serviceNameMatchCasing = ServiceListMonitor.Services.FirstOrDefault(s => s.Equals(ServiceName, StringComparison.InvariantCultureIgnoreCase));
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

        public INode[] GetNodes() => NodeMonitor?.Nodes ?? new INode[0];

        public bool WasUndeployed
        {
            get
            {
                if (_wasUndeployed)
                    return true;

                _wasUndeployed = !NodeMonitor.IsDeployed;
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
                ServiceListMonitor?.Dispose();
            }

            _disposed = true;
        }
    }
}
