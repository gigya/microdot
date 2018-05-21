using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summery>
    /// Monitors Consul using Health API and KeyValue API to find the current active version of a service,
    /// and provides a list of up-to-date, healthy nodes.
    /// </summery>
    internal class ConsulNodeSource: INodeSource
    {
        public bool SupportsMultipleEnvironments => true;

        private readonly object _initLocker = new object();
        private Task<ulong> _nodesInitTask;
        private Task<ulong> _versionInitTask;
        
        /// <summary>The currently active version of the service as obtained from the key-value store.</summary>
        private string _activeVersion = null;

        /// <summary>All of the service nodes, no matter the version.</summary>
        private Node[] _nodesOfAllVersions;

        /// <summary>The subset of the service nodes that match the current version specified in the KV store.</summary>
        private INode[] _nodes = new INode[0];

        /// <summary>Used by the VERSION loop to provide more details about NODES in exceptions.</summary>
        private ConsulResponse<Node[]> _lastNodesResponse;

        /// <summary>Used by the NODES loop to provide more details about VERSIONS in exceptions.</summary>
        private ConsulResponse<string> _lastVersionResponse;

        private ILog Log { get; }

        private Func<bool> IsDeployed { get; }

        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private string DataCenter { get; }
        private Task _nodesLoopTask;
        private Task _versionLoopTask;
        private CancellationTokenSource ShutdownToken { get; }
        internal EnvironmentException LastError { get; set; }
        private DateTime LastErrorTime { get; set; }
        
        private readonly DeploymentIdentifier _deploymentIdentifier;

        private int _stopped;
        private bool _wasUndeployed;

        public ConsulNodeSource(
            DeploymentIdentifier deploymentIdentifier,
            Func<bool> isDeployed,            
            ILog log,            
            ConsulClient consulClient,
            IEnvironmentVariableProvider environmentVariableProvider,
            IDateTime dateTime,
            Func<ConsulConfig> getConfig,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)

        {
            _deploymentIdentifier = deploymentIdentifier;
            IsDeployed = isDeployed;
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();            
            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");
        }

        /// <inheritdoc />
        public async Task Init()
        {
            var serviceExists = false;
            try
            {
                lock (_initLocker)
                {
                    if (_versionInitTask == null)
                    {
                        _versionInitTask = LoadVersion(0);
                        _nodesInitTask = LoadNodes(0);

                        _versionLoopTask = Task.Run(LoadVersionLoop);
                        _nodesLoopTask = Task.Run(LoadNodesLoop);
                    }
                }

                await Task.WhenAll(_nodesInitTask, _versionInitTask).ConfigureAwait(false);
            }
            catch (EnvironmentException ex)
            {
                LastError = ex;
            }
            finally
            {
                if (!_wasUndeployed)
                    InitHealthCheck();
            }
        }

        private string ActiveVersion
        {
            get => _activeVersion;
            set
            {
                _activeVersion = value;
                SetNodesByActiveVersion();
            }
        }

        private Node[] NodesOfAllVersions
        {
            get => _nodesOfAllVersions;
            set
            {
                _nodesOfAllVersions = value;
                SetNodesByActiveVersion();
            }
        }


        /// <inheritdoc />
        public INode[] GetNodes()
        {
            if (_nodes.Length == 0 && LastError != null)
            {
                if (LastError.StackTrace == null)
                    throw LastError;

                ExceptionDispatchInfo.Capture(LastError).Throw();
            }

            return _nodes;
        }

        /// <inheritdoc />
        public bool WasUndeployed
        {
            get
            {
                if (_wasUndeployed)
                    return true;

                if (!IsDeployed())
                    SetUndeployed();
                
                return _wasUndeployed;
            }
        }

        private void SetUndeployed()
        {
            _wasUndeployed = true;
            ShutdownToken.Cancel();
        }

        private async Task LoadVersionLoop()
        {
            try
            {
                var modifyIndex = await _versionInitTask.ConfigureAwait(false);
                while (!ShutdownToken.IsCancellationRequested)
                {
                    modifyIndex = await LoadVersion(modifyIndex).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }

        private async Task LoadNodesLoop()
        {
            try
            {
                var modifyIndex = await _nodesInitTask.ConfigureAwait(false);
                while (!ShutdownToken.IsCancellationRequested)
                {
                    modifyIndex = await LoadNodes(modifyIndex).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }

        private async Task<ulong> LoadVersion(ulong modifyIndex)
        {
            ConsulConfig config = GetConfig();

            await DateTime.DelayUntil(LastErrorTime + config.ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);
            
            _lastVersionResponse = await ConsulClient.GetDeploymentVersion(_deploymentIdentifier, modifyIndex, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastVersionResponse.Error != null)
            {
                ErrorResult(_lastVersionResponse);
            }
            if (_lastVersionResponse.IsUndeployed == true)
            {
                SetUndeployed();
            }
            else
            {
                string version = _lastVersionResponse.Result;

                if (version != null)
                {
                    ActiveVersion = version;
                    return _lastVersionResponse.ModifyIndex ?? 0;
                }
            }

            return 0;
        }

        private async Task<ulong> LoadNodes(ulong modifyIndex)
        {
            ConsulConfig config = GetConfig();

            await DateTime.DelayUntil(LastErrorTime + config.ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);

            _lastNodesResponse = await ConsulClient.GetHealthyNodes(_deploymentIdentifier, modifyIndex, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastNodesResponse.Error != null)
            {
                ErrorResult(_lastNodesResponse);
            }
            else
            {
                NodesOfAllVersions = _lastNodesResponse.Result;
                return _lastNodesResponse.ModifyIndex ?? 0;
            }
            return 0;
        }

        private void SetNodesByActiveVersion()
        {
            if (ActiveVersion != null && NodesOfAllVersions != null)
            {
                _nodes = NodesOfAllVersions.Where(n => n.Version == ActiveVersion).ToArray();
                if (_nodes.Length == 0)
                    ErrorResult(_lastNodesResponse, "No endpoints were specified in Consul for the requested service and service's active version.");
                else
                    LastError = null;
            }
        }

        private void ErrorResult<T>(ConsulResponse<T> response, string message = null)
        {
            EnvironmentException error = response.Error ?? new EnvironmentException(message);

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error(message ?? "Consul error", exception: response?.Error, unencryptedTags: new
                {
                    serviceName = _deploymentIdentifier.ServiceName,
                    serviceEnv = _deploymentIdentifier.DeploymentEnvironment,
                    consulAddress = response?.ConsulAddress,
                    commandPath = response?.CommandPath,
                    responseCode = response?.StatusCode,
                    content = response?.ResponseContent,
                    activeVersion = ActiveVersion,

                    lastVersionResponseCode = _lastVersionResponse?.StatusCode,
                    lastVersionCommand = _lastVersionResponse?.CommandPath,
                    lastVersionResponse = _lastVersionResponse?.ResponseContent,

                    lastNodesResponseCode = _lastNodesResponse?.StatusCode,
                    lastNodesCommand = _lastNodesResponse?.CommandPath,
                    lastNodesResponse = _lastNodesResponse?.ResponseContent,
                });
            }

            LastError = new EnvironmentException(message ?? "Consul error", error, unencrypted: new Tags{
                { "serviceName", _deploymentIdentifier.ServiceName},
                { "serviceEnv", _deploymentIdentifier.DeploymentEnvironment},
                { "consulAddress", response?.ConsulAddress},
                { "commandPath", response?.CommandPath},
                { "responseCode", response?.StatusCode?.ToString()},
                { "content", response?.ResponseContent},
                { "activeVersion", ActiveVersion},

                { "lastVersionResponseCode", _lastVersionResponse?.StatusCode.ToString() },
                { "lastVersionCommand", _lastVersionResponse?.CommandPath},
                { "lastVersionResponse", _lastVersionResponse?.ResponseContent},

                { "lastNodesResponseCode", _lastNodesResponse?.StatusCode.ToString() },
                { "lastNodesCommand", _lastNodesResponse?.CommandPath},
                { "lastNodesResponse", _lastNodesResponse?.ResponseContent}
            });
            LastErrorTime = DateTime.UtcNow;
        }

        private void InitHealthCheck()
        {
            AggregatingHealthStatus.RegisterCheck(_deploymentIdentifier.ToString(), CheckHealth);
        }

        private HealthCheckResult CheckHealth()
        {
            if (WasUndeployed)
                HealthCheckResult.Healthy($"Not exists on Consul (key value store)");

            var error = _lastNodesResponse?.Error ?? _lastVersionResponse?.Error ?? LastError;
            if (error != null)
            {
                return HealthCheckResult.Unhealthy($"Consul error: " + error.Message);
            }

            string healthMessage;

            var nodes = GetNodes();
            if (nodes.Length < NodesOfAllVersions?.Length)
                healthMessage = $"{nodes.Length} nodes matching to version {ActiveVersion} from total of {NodesOfAllVersions.Length} nodes";
            else
                healthMessage = $"{nodes.Length} nodes";

            return HealthCheckResult.Healthy(healthMessage);
        }


        public void Shutdown()
        {
            if (Interlocked.Increment(ref _stopped) != 1)
                return;

            AggregatingHealthStatus.RemoveCheck(_deploymentIdentifier.ToString());

            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }

    }
}
