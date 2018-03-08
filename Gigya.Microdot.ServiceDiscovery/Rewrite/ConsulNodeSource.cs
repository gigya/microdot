using System;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulNodeSource: INodeSource
    {
        private readonly ConsulServiceState _serviceState;

        public virtual string Type => "Consul";

        public bool SupportsMultipleEnvironments => true;

        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private ServiceDeployment ServiceDeployment { get; }
        private IConsulClient ConsulClient { get; }
        private Func<ConsulConfig> GetConfig { get; }        
        private CancellationTokenSource ShutdownToken { get; }

        private bool _disposed;
        private INode[] _lastKnownNodes;
        private bool _isActive;

        private readonly object _initLock = new object();
        private TaskCompletionSource<bool> _nodesInitialized;

        public ConsulNodeSource(ServiceDeployment serviceDeployment,
            IConsulClient consulClient,
            Func<ConsulConfig> getConfig,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            ConsulClient = consulClient;
            GetConfig = getConfig;            
            ServiceDeployment = serviceDeployment;
            ShutdownToken = new CancellationTokenSource();

            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");
            _serviceState = new ConsulServiceState($"{serviceDeployment.ServiceName}-{serviceDeployment.DeploymentEnvironment}");
            AggregatingHealthStatus.RegisterCheck(_serviceState.ServiceName, CheckHealth);            
        }

        public async Task Init()
        {
            lock (_initLock)
            {
                if (_nodesInitialized == null)
                {
                    _nodesInitialized = new TaskCompletionSource<bool>();
                    LoadNodesLoop();
                }
            }
            _nodesInitialized.Task.Wait(GetConfig().InitializationTimeout);
        }

        private async Task LoadNodesLoop()
        {
            while (ShutdownToken.IsCancellationRequested == false)
            {
                await ConsulClient.LoadNodes(_serviceState).ConfigureAwait(false);

                _isActive = _serviceState.IsDeployed;
                var nodes = _serviceState.Nodes;

                if (nodes.Length == 0 && _serviceState.LastResult?.Error != null && _isActive && _lastKnownNodes != null)
                    nodes = _lastKnownNodes;

                _lastKnownNodes = nodes;

                _nodesInitialized.TrySetResult(true);
            }
        }

        public INode[] GetNodes()
        {            
            return _lastKnownNodes;
        }

        public bool WasUndeployed => !_isActive;

        private HealthCheckResult CheckHealth()
        {
            string healthMessage = null;
            if (!_serviceState.IsDeployed)
                return HealthCheckResult.Healthy($"{_serviceState.ServiceNameOrigin} - Not exists on Consul");

            var error = _serviceState.LastResult?.Error;
            if (error != null)
            {
                return HealthCheckResult.Unhealthy($"{_serviceState.ServiceNameOrigin} - Consul error: " + error.Message);
            }

            if (_serviceState.Nodes.Length < _serviceState.NodesOfAllVersions.Length)
                healthMessage = $"{_serviceState.Nodes.Length} nodes matching to version {_serviceState.ActiveVersion} from total of {_serviceState.NodesOfAllVersions.Length} nodes";
            else
                healthMessage = $"{_serviceState.Nodes.Length} nodes";

            if (_serviceState.ServiceName == _serviceState.ServiceNameOrigin)
                return HealthCheckResult.Healthy($"{_serviceState.ServiceNameOrigin} - {healthMessage}");
            else
                return HealthCheckResult.Healthy($"{_serviceState.ServiceNameOrigin} - Service exists on Consul, but with different casing: '{_serviceState.ServiceName}'. {healthMessage}");
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
                ShutdownToken.Cancel();
                ShutdownToken.Dispose();
                _serviceState.Dispose();
            }

            _disposed = true;
        }
    }
}
