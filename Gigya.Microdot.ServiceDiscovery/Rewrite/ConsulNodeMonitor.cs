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
    /// and provides a list of healthy nodes.
    /// </summery>
    public sealed class ConsulNodeMonitor : INodeMonitor
    {
        private int _disposed;
        private string _activeVersion;
        private Node[] _nodesOfAllVersions = new Node[0];
        private INode[] _nodes = new INode[0];
        private Task<ulong> _nodesInitTask;
        private Task<ulong> _versionInitTask;
        private ConsulResult<ServiceEntry[]> _lastNodesResult;
        private ConsulResult<KeyValueResponse[]> _lastVersionResult;

        private ILog Log { get; }
        private IServiceListMonitor ServiceListMonitor { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private string DataCenter { get; }
        private Task NodesLoopTask { get; set; }
        private Task VersionLoopTask { get; set; }
        private CancellationTokenSource ShutdownToken { get; }

        public ConsulNodeMonitor(
            string deploymentIdentifier, 
            ILog log, 
            IServiceListMonitor serviceListMonitor, 
            ConsulClient consulClient, 
            IEnvironmentVariableProvider environmentVariableProvider, 
            IDateTime dateTime,
            Func<ConsulConfig> getConfig, 
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            DeploymentIdentifier = deploymentIdentifier;
            Log = log;
            ServiceListMonitor = serviceListMonitor;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();
            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");
            AggregatingHealthStatus.RegisterCheck(DeploymentIdentifier, CheckHealth);

            VersionLoopTask = LoadVersionLoop();
            NodesLoopTask = LoadNodesLoop();
        }

        private string DeploymentIdentifier { get; }

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

                if (!IsDeployed)
                    throw Ex.ServiceNotDeployed(DataCenter, DeploymentIdentifier);

                if (_nodes.Length == 0 && Error != null)
                {
                    if (Error.StackTrace == null)
                        throw Error;

                    ExceptionDispatchInfo.Capture(Error).Throw();
                }

                return _nodes;
            }
        }

        /// <inheritdoc />
        public bool IsDeployed => ServiceListMonitor.Services.Contains(DeploymentIdentifier);

        private EnvironmentException Error { get; set; }
        private DateTime ErrorTime { get; set; }

        /// <inheritdoc />
        public Task Init() => Task.WhenAll(_nodesInitTask, _versionInitTask);

        private async Task LoadVersionLoop()
        {
            _versionInitTask = LoadVersion(0);
            var modifyIndex = await _versionInitTask.ConfigureAwait(false);            
            while (!ShutdownToken.IsCancellationRequested)
            {
                modifyIndex = await LoadVersion(modifyIndex).ConfigureAwait(false);
            }
        }

        private async Task LoadNodesLoop()
        {
            _nodesInitTask = LoadNodes(0);
            var modifyIndex = await _nodesInitTask.ConfigureAwait(false);
            while (!ShutdownToken.IsCancellationRequested)
            {
                modifyIndex = await LoadNodes(modifyIndex).ConfigureAwait(false);
            }
        }

        private async Task<ulong> LoadVersion(ulong modifyIndex)
        {
            ConsulConfig config = GetConfig();
           
            if (Error != null)
                await DateTime.DelayUntil(ErrorTime + config.ErrorRetryInterval).ConfigureAwait(false);

            double maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            string urlCommand = $"v1/kv/service/{DeploymentIdentifier}?dc={DataCenter}&index={modifyIndex}&wait={maxSecondsToWaitForResponse}s";
            _lastVersionResult = await ConsulClient.Call<KeyValueResponse[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastVersionResult.IsDeployed && _lastVersionResult.IsSuccessful)
            {
                string version = _lastVersionResult.Response?.SingleOrDefault()?.TryDecodeValue()?.Version;

                if (version != null)
                {
                    ActiveVersion = version;
                    return _lastVersionResult.ModifyIndex ?? 0;
                }
            }
            
            ErrorResult(_lastVersionResult, "Cannot extract service's active version from Consul response");
            return 0;
        }

        private async Task<ulong> LoadNodes(ulong modifyIndex)
        {
            ConsulConfig config = GetConfig();
          
            if (Error != null)
                await DateTime.DelayUntil(ErrorTime + config.ErrorRetryInterval).ConfigureAwait(false);

            double maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            string urlCommand = $"v1/health/service/{DeploymentIdentifier}?dc={DataCenter}&passing&index={modifyIndex}&wait={maxSecondsToWaitForResponse}s";
            _lastNodesResult = await ConsulClient.Call<ServiceEntry[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastNodesResult.IsSuccessful)
            {
                var consulNodes = _lastNodesResult.Response;
                if (consulNodes != null)
                {
                    NodesOfAllVersions = consulNodes.Select(n => n.ToNode()).ToArray();
                    return _lastNodesResult.ModifyIndex ?? 0;
                }
            }            
            ErrorResult(_lastNodesResult, "Cannot extract service's nodes from Consul response");
            return 0;
        }

        private void SetNodesByActiveVersion()
        {
            if (ActiveVersion != null)
            {
                _nodes = NodesOfAllVersions.Where(n => n.Version == ActiveVersion).ToArray();
                if (_nodes.Length==0)
                    ErrorResult(_lastNodesResult, "No endpoints were specified in Consul for the requested service and service's active version.");
                else
                    Error = null;
            }
        }

        private void ErrorResult<T>(ConsulResult<T> result, string errorMessage)
        {
            EnvironmentException error = result?.Error ?? new EnvironmentException(errorMessage);

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error("Error calling Consul", exception: result?.Error, unencryptedTags: new
                {
                    serviceName = DeploymentIdentifier,
                    consulAddress = result?.ConsulAddress,
                    commandPath = result?.CommandPath,
                    responseCode = result?.StatusCode,
                    content = result?.ResponseContent,
                    activeVersion = ActiveVersion,
                    lastVersionCommand = _lastVersionResult?.CommandPath,
                    lastVersionResponse = _lastVersionResult?.ResponseContent,
                    lastNodesCommand = _lastNodesResult?.CommandPath,
                    lastNodesResponse = _lastNodesResult?.ResponseContent,
                });
            }

            Error = error;
            ErrorTime = DateTime.UtcNow;
        }


        private HealthCheckResult CheckHealth()
        {
            if (!IsDeployed)
                HealthCheckResult.Healthy($"Not exists on Consul");

            var error = _lastNodesResult?.Error ?? _lastVersionResult?.Error ?? Error;
            if (error != null)
            {
                return HealthCheckResult.Unhealthy($"Consul error: " + error.Message);
            }

            string healthMessage;
            
            if (Nodes.Length < NodesOfAllVersions.Length)
                healthMessage = $"{Nodes.Length} nodes matching to version {ActiveVersion} from total of {NodesOfAllVersions.Length} nodes";
            else
                healthMessage = $"{Nodes.Length} nodes";

            return HealthCheckResult.Healthy(healthMessage);
        }


        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            AggregatingHealthStatus.RemoveCheck(DeploymentIdentifier);
            ShutdownToken?.Cancel();
            NodesLoopTask.GetAwaiter().GetResult();
            VersionLoopTask.GetAwaiter().GetResult();
            ShutdownToken?.Dispose();
        }
    }
}

