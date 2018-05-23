using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
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
        private readonly object _initLocker = new object();
        private Task<ConsulResponse<ConsulNode[]>> _nodesInitTask;
        private Task<ConsulResponse<string>> _versionInitTask;

        /// <summary>The subset of the service nodes that match the current version specified in the KV store.</summary>
        private Node[] _nodes = new Node[0];

        /// <summary>Used by the VERSION loop to provide more details about NODES in exceptions.</summary>
        private ConsulResponse<ConsulNode[]> _lastNodesResponse;

        /// <summary>Used by the NODES loop to provide more details about VERSIONS in exceptions.</summary>
        private ConsulResponse<string> _lastVersionResponse;

        private ILog Log { get; }

        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private CancellationTokenSource ShutdownToken { get; }
        internal EnvironmentException LastError { get; set; }
        private DateTime LastErrorTime { get; set; }
        
        private readonly DeploymentIdentifier _deploymentIdentifier;

        private int _stopped;
        private bool _wasUndeployed;
        private TaskCompletionSource<bool> _initCompleted;

        public ConsulNodeSource(
            DeploymentIdentifier deploymentIdentifier,
            ILog log,            
            ConsulClient consulClient,
            IDateTime dateTime,
            Func<ConsulConfig> getConfig,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)

        {
            _deploymentIdentifier = deploymentIdentifier;            
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            ShutdownToken = new CancellationTokenSource();            
            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");
        }

        /// <inheritdoc />
        public async Task Init()
        {
            try
            {
                lock (_initLocker)
                {
                    if (_versionInitTask == null)
                    {
                        InitHealthCheck();

                        _versionInitTask = LoadVersion(0, ShutdownToken.Token);
                        _nodesInitTask = LoadNodes(0, ShutdownToken.Token);
                        _initCompleted = new TaskCompletionSource<bool>();
                        Task.Run(LoadVersionNodesLoop);
                    }
                }

                await _initCompleted.Task.ConfigureAwait(false);
            }
            catch (EnvironmentException ex)
            {
                LastError = ex;
            }
        }

        private string ActiveVersion => _lastVersionResponse?.Result;

        private ConsulNode[] NodesOfAllVersions => _lastNodesResponse?.Result;


        /// <inheritdoc />
        public Node[] GetNodes()
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

                return _wasUndeployed;
            }
        }

        private async Task LoadVersionNodesLoop()
        {
            try
            {
                var versionResponse = await _versionInitTask.ConfigureAwait(false);
                var nodesResponse = await _nodesInitTask.ConfigureAwait(false);
                while (!ShutdownToken.IsCancellationRequested)
                {
                    if (versionResponse.IsUndeployed == true)
                    {
                        _wasUndeployed = true;
                        ShutdownToken.Cancel();
                        return;
                    }

                    var version = versionResponse.Result;
                    var nodes = nodesResponse.Result;

                    if (version == null)
                        LogError(versionResponse, "Cannot obtain active version for the requested service");
                    else if (nodes != null)
                    {
                        _nodes = nodes.Where(n => n.Version == version).ToArray();
                        if (_nodes.Length == 0)
                            LogError(nodesResponse, "No nodes were specified in Consul for the requested service and service's active version.");
                        else
                            LastError = null;
                    }
                    _initCompleted.TrySetResult(true);

                    using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(ShutdownToken.Token))
                    {
                        var loadVersion = LoadVersion(versionResponse.ModifyIndex, cancellationSource.Token);
                        var loadNodes = LoadNodes(versionResponse.ModifyIndex, cancellationSource.Token);

                        await Task.WhenAny(loadVersion, loadNodes).ConfigureAwait(false);
                        cancellationSource.Cancel();

                        var newVersionResponse = await loadVersion.ConfigureAwait(false);
                        if (newVersionResponse.Error == null)
                            versionResponse = newVersionResponse;

                        var newNodesResponse = await loadNodes.ConfigureAwait(false);
                        if (newNodesResponse.Error == null)
                            nodesResponse = newNodesResponse;
                    }

                    await DateTime.DelayUntil(LastErrorTime + GetConfig().ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }


        private async Task<ConsulResponse<string>> LoadVersion(ulong? modifyIndex, CancellationToken cancellationToken)
        {
            _lastVersionResponse = await ConsulClient.GetDeploymentVersion(_deploymentIdentifier, modifyIndex ?? 0, cancellationToken).ConfigureAwait(false);

            if (_lastVersionResponse.Error != null)
            {
                LogError(_lastVersionResponse);
            }

            return _lastVersionResponse;
        }

        private async Task<ConsulResponse<ConsulNode[]>> LoadNodes(ulong? modifyIndex, CancellationToken cancellationToken)
        {
            _lastNodesResponse = await ConsulClient.GetHealthyNodes(_deploymentIdentifier, modifyIndex ?? 0, cancellationToken).ConfigureAwait(false);

            if (_lastNodesResponse.Error != null)
            {
                LogError(_lastNodesResponse);
            }

            return _lastNodesResponse;
        }


        private void LogError<T>(ConsulResponse<T> response, string message = null)
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


                LastError = new EnvironmentException(message ?? "Consul error", error, unencrypted: new Tags
                {
                    {"serviceName", _deploymentIdentifier.ServiceName},
                    {"serviceEnv", _deploymentIdentifier.DeploymentEnvironment},
                    {"consulAddress", response?.ConsulAddress},
                    {"commandPath", response?.CommandPath},
                    {"responseCode", response?.StatusCode?.ToString()},
                    {"content", response?.ResponseContent},
                    {"activeVersion", ActiveVersion},

                    {"lastVersionResponseCode", _lastVersionResponse?.StatusCode.ToString()},
                    {"lastVersionCommand", _lastVersionResponse?.CommandPath},
                    {"lastVersionResponse", _lastVersionResponse?.ResponseContent},

                    {"lastNodesResponseCode", _lastNodesResponse?.StatusCode.ToString()},
                    {"lastNodesCommand", _lastNodesResponse?.CommandPath},
                    {"lastNodesResponse", _lastNodesResponse?.ResponseContent}
                });
                LastErrorTime = DateTime.UtcNow;
            }
        }

        private void InitHealthCheck()
        {
            AggregatingHealthStatus.RegisterCheck(_deploymentIdentifier.ToString(), CheckHealth);
        }

        private HealthCheckResult CheckHealth()
        {
            if (WasUndeployed) 
                return HealthCheckResult.Unhealthy($"Not exists on Consul (key value store)");

            var possibleErrors = new[] {_lastNodesResponse?.Error, _lastVersionResponse?.Error, LastError}.Where(e=>e!=null);
            var error = possibleErrors.FirstOrDefault(e=>!(e.InnerException is TaskCanceledException));
            if (error != null)
            {
                return HealthCheckResult.Unhealthy("Consul error: " + error.RawMessage);
            }

            string healthMessage;
            
            if (_nodes.Length < NodesOfAllVersions?.Length)
                healthMessage = $"{_nodes.Length} nodes matching to version {ActiveVersion} from total of {NodesOfAllVersions.Length} nodes";
            else
                healthMessage = $"{_nodes.Length} nodes";

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
