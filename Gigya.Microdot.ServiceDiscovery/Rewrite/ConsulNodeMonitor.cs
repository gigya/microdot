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
    public sealed class ConsulNodeMonitor : INodeMonitor
    {
        private int _disposed;
        private int _initiated;
        private readonly TaskCompletionSource<bool> _waitForNodesInitiation = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> _waitForVersionInitiation = new TaskCompletionSource<bool>();
        private Task<ulong> _nodesInitTask;
        private Task<ulong> _versionInitTask;

        private bool _wasUndeployed;
        private string _activeVersion; // the currently active version of the service as obtained from the key-value store.
        private Node[] _nodesOfAllVersions;
        private INode[] _nodes = new INode[0];
        private ConsulResult<ServiceEntry[]> _lastNodesResult;
        private ConsulResult<KeyValueResponse[]> _lastVersionResult;

        private ILog Log { get; }
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
        private DeploymentIdentifier DeploymentIdentifier { get; }

        public ConsulNodeMonitor(
            DeploymentIdentifier deploymentIdentifier, 
            ILog log, 
            IConsulServiceListMonitor consulServiceListMonitor, 
            ConsulClient consulClient, 
            IEnvironmentVariableProvider environmentVariableProvider, 
            IDateTime dateTime,
            Func<ConsulConfig> getConfig, 
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            DeploymentIdentifier = deploymentIdentifier;
            Log = log;
            ConsulServiceListMonitor = consulServiceListMonitor;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();
            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");
            AggregatingHealthStatus.RegisterCheck(DeploymentIdentifier.ToString(), CheckHealth);
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
        public INode[] Nodes
        {
            get
            {
                if (_disposed > 0)
                    throw new ObjectDisposedException(nameof(ConsulNodeMonitor));

                if (_nodes.Length == 0 && LastError != null)
                {
                    if (LastError.StackTrace == null)
                        throw LastError;

                    ExceptionDispatchInfo.Capture(LastError).Throw();
                }

                return _nodes;
            }
        }

        /// <inheritdoc />
        public bool WasUndeployed
        {
            get
            {
                if (_wasUndeployed)
                    return true;

                _wasUndeployed = !ConsulServiceListMonitor.ServiceExists(DeploymentIdentifier, out var _);
                if (_wasUndeployed)
                    ShutdownToken?.Cancel();

                return _wasUndeployed;
            }
        }


        /// <inheritdoc />
        public async Task Init()
        {
            await ConsulServiceListMonitor.Init().ConfigureAwait(false);

            if (Interlocked.Increment(ref _initiated) == 1)
            {
                _versionLoopTask = LoadVersionLoop();
                _nodesLoopTask = LoadNodesLoop();
                await Task.WhenAll(_waitForNodesInitiation.Task, _waitForVersionInitiation.Task).ConfigureAwait(false);
            }

            await Task.WhenAll(_nodesInitTask, _versionInitTask).ConfigureAwait(false);
        }

        private async Task LoadVersionLoop()
        {
            try
            {
                _versionInitTask = LoadVersion(0);
                _waitForVersionInitiation.TrySetResult(true);

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
                _nodesInitTask = LoadNodes(0);
                _waitForNodesInitiation.TrySetResult(true);

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

            string urlCommand = $"v1/kv/service/{DeploymentIdentifier}?dc={DataCenter}&index={modifyIndex}&wait={config.HttpTimeout.TotalSeconds}s";
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

            string urlCommand = $"v1/health/service/{DeploymentIdentifier}?dc={DataCenter}&passing&index={modifyIndex}&wait={config.HttpTimeout.TotalSeconds}s";
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

        private void ErrorResult<T>(ConsulResult<T> result, string message=null)
        {
            EnvironmentException error = result.Error ?? new EnvironmentException(message);

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error(message ?? "Consul error", exception: result?.Error, unencryptedTags: new
                {
                    serviceName   = DeploymentIdentifier.ServiceName,
                    serviceEnv   = DeploymentIdentifier.DeploymentEnvironment,
                    consulAddress = result?.ConsulAddress,
                    commandPath   = result?.CommandPath,
                    responseCode  = result?.StatusCode,
                    content       = result?.ResponseContent,
                    activeVersion = ActiveVersion,

                    lastVersionResponseCode = _lastVersionResult?.StatusCode,
                    lastVersionCommand      = _lastVersionResult?.CommandPath,
                    lastVersionResponse     = _lastVersionResult?.ResponseContent,

                    lastNodesResponseCode = _lastNodesResult?.StatusCode,
                    lastNodesCommand      = _lastNodesResult?.CommandPath,
                    lastNodesResponse     = _lastNodesResult?.ResponseContent,
                });
            }
            
            LastError = new EnvironmentException(message?? "Consul error", error, unencrypted: new Tags{
                { "serviceName", DeploymentIdentifier.ServiceName},
                { "serviceEnv", DeploymentIdentifier.DeploymentEnvironment},
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
            
            if (Nodes.Length < NodesOfAllVersions?.Length)
                healthMessage = $"{Nodes.Length} nodes matching to version {ActiveVersion} from total of {NodesOfAllVersions.Length} nodes";
            else
                healthMessage = $"{Nodes.Length} nodes";

            return HealthCheckResult.Healthy(healthMessage);
        }


        public void Dispose()
        {
            DisposeAsync().Wait(TimeSpan.FromSeconds(3));
        }

        public async Task DisposeAsync()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            AggregatingHealthStatus.RemoveCheck(DeploymentIdentifier.ToString());

            ShutdownToken?.Cancel();
            if (_nodesLoopTask!=null)
                await _nodesLoopTask.ConfigureAwait(false);
            if (_versionLoopTask!=null)
                await _versionLoopTask.ConfigureAwait(false);

            ShutdownToken?.Dispose();
        }
    }
}

