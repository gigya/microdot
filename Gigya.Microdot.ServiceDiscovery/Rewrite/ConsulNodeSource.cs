using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulNodeSource: INodeSource
    {
        public virtual string Type => "Consul";

        public bool SupportsMultipleEnvironments => true;

        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private ServiceDeployment ServiceDeployment { get; }
        private IConsulServiceListMonitor ServiceListMonitor { get; }
        private Func<string, IConsulNodeMonitor> GetNodeMonitor { get; }
        private IConsulNodeMonitor NodeMonitor { get; set; }             
        public string ServiceName { get; private set; }

        private bool _disposed;
        private INode[] _lastKnownNodes;
        private bool _isActive;

        public ConsulNodeSource(ServiceDeployment serviceDeployment,
            IConsulServiceListMonitor serviceListMonitor,
            Func<string, IConsulNodeMonitor> getNodeMonitor,            
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            ServiceListMonitor = serviceListMonitor;
            GetNodeMonitor = getNodeMonitor;
            ServiceName = $"{serviceDeployment.ServiceName}-{serviceDeployment.DeploymentEnvironment}";

            ServiceDeployment = serviceDeployment;

            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");            
        }

        public async Task Init()
        {
            await ServiceListMonitor.Init().ConfigureAwait(false);            

            var serviceNameMatchCasing = ServiceListMonitor.Services.FirstOrDefault(s=>s.Equals(ServiceName, StringComparison.InvariantCultureIgnoreCase));
            if (serviceNameMatchCasing != null)
            {
                ServiceName = serviceNameMatchCasing;
                NodeMonitor = GetNodeMonitor(ServiceName);
                await NodeMonitor.Init().ConfigureAwait(false);
            }
        }

        public INode[] GetNodes()
        {            
            return NodeMonitor.Nodes;
        }

        public bool WasUndeployed
        {
            get
            {
                if (ServiceListMonitor.Services.Contains(ServiceName))
                    return false;

                AggregatingHealthStatus.RegisterCheck(ServiceName, ()=> HealthCheckResult.Healthy($"Not exists on Consul"));
                return true;
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
                AggregatingHealthStatus?.RemoveCheck(ServiceName);
                NodeMonitor?.Dispose();
                ServiceListMonitor?.Dispose();
            }

            _disposed = true;
        }
    }
}
