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
        /// <inheritdoc />
        public virtual string Type => "Consul";

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
        private ConsulResult<ServiceEntry[]> _lastNodesResult;

        /// <summary>Used by the NODES loop to provide more details about VERSIONS in exceptions.</summary>
        private ConsulResult<KeyValueResponse[]> _lastVersionResult;

        private ILog Log { get; }

        /// <summary>We use this to efficiently detect if the service was 
        /// 
        /// </summary>
        private IConsulServiceListMonitor ConsulServiceListMonitor { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private string DataCenter { get; }
        private Task _nodesLoopTask;
        private Task _versionLoopTask;
        private CancellationTokenSource ShutdownToken { get; }
        private EnvironmentException LastError { get; set; }
        private DateTime LastErrorTime { get; set; }
        
        private DeploymentIdentifier _deploymentIdentifier;

        private int _stopped;
        private bool _wasUndeployed;

        public ConsulNodeSource(DeploymentIdentifier deploymentIdentifier,
            IConsulServiceListMonitor consulServiceListMonitor,            
            ILog log,            
            ConsulClient consulClient,
            IEnvironmentVariableProvider environmentVariableProvider,
            IDateTime dateTime,
            Func<ConsulConfig> getConfig,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)

        {
            ConsulServiceListMonitor = consulServiceListMonitor;            
            _deploymentIdentifier = deploymentIdentifier;

            Log = log;
            ConsulServiceListMonitor = consulServiceListMonitor;
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
            await ConsulServiceListMonitor.Init().ConfigureAwait(false);

            var serviceExists = false;
            try
            {

                if (ConsulServiceListMonitor.ServiceExists(_deploymentIdentifier,
                    out var deploymentIdentifierMatchCasing))
                {
                    // TODO: Remove if consul is guaranteed to be with correct casing
                    _deploymentIdentifier = deploymentIdentifierMatchCasing;

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
                else
                    _wasUndeployed = true;
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

                // we do this to get the correct casing of the service name. TODO: can we rely on consul to use correct casings and remove the dependency on the services list
                // and use some narrower API such as IsServiceExists(string serviceName) instead?
                _wasUndeployed = !(ConsulServiceListMonitor.ServiceExists(_deploymentIdentifier, out var deploymentIdentifierMatchCasing));
                // TODO: Remove if consul is guaranteed to be with correct casing
                if (deploymentIdentifierMatchCasing != _deploymentIdentifier)
                    _wasUndeployed = true;

                return _wasUndeployed;
            }
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

            string urlCommand = $"v1/kv/service/{_deploymentIdentifier}?dc={DataCenter}&index={modifyIndex}&wait={config.HttpTimeout.TotalSeconds}s";
            _lastVersionResult = await ConsulClient.Call<KeyValueResponse[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastVersionResult.Error != null)
            {
                ErrorResult(_lastVersionResult);
            }
            else if (_lastVersionResult.IsUndeployed == true || _lastVersionResult.Response == null)
            {
                ErrorResult(_lastVersionResult, "Unexpected result from Consul");
                // This situation is ignored because other processes are responsible for indicating when a service is undeployed.
            }
            else
            {
                string version = _lastVersionResult.Response?.SingleOrDefault()?.TryDecodeValue()?.Version;

                if (version != null)
                {
                    ActiveVersion = version;
                    return _lastVersionResult.ModifyIndex ?? 0;
                }
            }

            return 0;
        }

        private async Task<ulong> LoadNodes(ulong modifyIndex)
        {
            ConsulConfig config = GetConfig();

            await DateTime.DelayUntil(LastErrorTime + config.ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);

            string urlCommand = $"v1/health/service/{_deploymentIdentifier}?dc={DataCenter}&passing&index={modifyIndex}&wait={config.HttpTimeout.TotalSeconds}s";
            _lastNodesResult = await ConsulClient.Call<ServiceEntry[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastNodesResult.Error != null)
            {
                ErrorResult(_lastNodesResult);
            }
            else if (_lastNodesResult.IsUndeployed == true || _lastNodesResult.Response == null)
            {
                ErrorResult(_lastNodesResult, "Unexpected result from Consul");
                // TODO: if _lastNodesResult.IsUndeployed then we probably should also _wasUndeployed = true;
            }
            else
            {
                NodesOfAllVersions = _lastNodesResult.Response.Select(n => n.ToNode()).ToArray();
                return _lastNodesResult.ModifyIndex ?? 0;
            }
            return 0;
        }

        private void SetNodesByActiveVersion()
        {
            if (ActiveVersion != null && NodesOfAllVersions != null)
            {
                _nodes = NodesOfAllVersions.Where(n => n.Version == ActiveVersion).ToArray();
                if (_nodes.Length == 0)
                    ErrorResult(_lastNodesResult, "No endpoints were specified in Consul for the requested service and service's active version.");
                else
                    LastError = null;
            }
        }

        private void ErrorResult<T>(ConsulResult<T> result, string message = null)
        {
            EnvironmentException error = result.Error ?? new EnvironmentException(message);

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error(message ?? "Consul error", exception: result?.Error, unencryptedTags: new
                {
                    serviceName = _deploymentIdentifier.ServiceName,
                    serviceEnv = _deploymentIdentifier.DeploymentEnvironment,
                    consulAddress = result?.ConsulAddress,
                    commandPath = result?.CommandPath,
                    responseCode = result?.StatusCode,
                    content = result?.ResponseContent,
                    activeVersion = ActiveVersion,

                    lastVersionResponseCode = _lastVersionResult?.StatusCode,
                    lastVersionCommand = _lastVersionResult?.CommandPath,
                    lastVersionResponse = _lastVersionResult?.ResponseContent,

                    lastNodesResponseCode = _lastNodesResult?.StatusCode,
                    lastNodesCommand = _lastNodesResult?.CommandPath,
                    lastNodesResponse = _lastNodesResult?.ResponseContent,
                });
            }

            LastError = new EnvironmentException(message ?? "Consul error", error, unencrypted: new Tags{
                { "serviceName", _deploymentIdentifier.ServiceName},
                { "serviceEnv", _deploymentIdentifier.DeploymentEnvironment},
                { "consulAddress", result?.ConsulAddress},
                { "commandPath", result?.CommandPath},
                { "responseCode", result?.StatusCode?.ToString()},
                { "content", result?.ResponseContent},
                { "activeVersion", ActiveVersion},

                { "lastVersionResponseCode", _lastVersionResult?.StatusCode.ToString() },
                { "lastVersionCommand", _lastVersionResult?.CommandPath},
                { "lastVersionResponse", _lastVersionResult?.ResponseContent},

                { "lastNodesResponseCode", _lastNodesResult?.StatusCode.ToString() },
                { "lastNodesCommand", _lastNodesResult?.CommandPath},
                { "lastNodesResponse", _lastNodesResult?.ResponseContent}
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

            var error = _lastNodesResult?.Error ?? _lastVersionResult?.Error ?? LastError;
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
