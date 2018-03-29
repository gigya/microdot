using System;
using System.Linq;
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
    public sealed class ConsulNodeMonitor: INodeMonitor
    {
        private CancellationTokenSource ShutdownToken { get; }

        private int _disposed;

        private string _activeVersion;
        private Node[] _nodesOfAllVersions = new Node[0];
        private INode[] _nodes = new INode[0];
        private Task<ulong> _nodesInitTask;
        private Task<ulong> _versionInitTask;
        private ConsulResult<ServiceEntry[]> _lastNodesResult;
        private ConsulResult<KeyValueResponse[]> _lastVersionResult;

        ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        public ConsulNodeMonitor(string serviceName, ILog log, ConsulClient consulClient, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig, Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)            
        {
            ServiceName = serviceName;
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();
            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient");
            AggregatingHealthStatus.RegisterCheck(ServiceName, CheckHealth);

            LoadVersionLoop();
            LoadNodesLoop();
        }

        public string DataCenter { get; }

        private string ServiceName { get; }

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

        public INode[] Nodes
        {
            get
            {
                if (_nodes.Length == 0 && Error != null)
                    throw Error;
                return _nodes;
            }
            set => _nodes = value;
        }

        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

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
            await WaitIfErrorOccuredOnPreviousCall().ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/kv/service/{ServiceName}?dc={DataCenter}&index={modifyIndex}&wait={maxSecondsToWaitForResponse}s";
            _lastVersionResult = await ConsulClient.Call<KeyValueResponse[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastVersionResult.IsDeployed && _lastVersionResult.Success)
            {
                var version = _lastVersionResult.Response?.SingleOrDefault()?.TryDecodeValue()?.Version;

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
            await WaitIfErrorOccuredOnPreviousCall().ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/health/service/{ServiceName}?dc={DataCenter}&passing&index={modifyIndex}&wait={maxSecondsToWaitForResponse}s";
            _lastNodesResult = await ConsulClient.Call<ServiceEntry[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (_lastNodesResult.Success)
            {
                var consulNodes = _lastNodesResult.Response;
                if (consulNodes != null)
                {
                    NodesOfAllVersions = ConsulClient.ReadConsulNodes(consulNodes);
                    return _lastNodesResult.ModifyIndex ?? 0;
                }
            }            
            ErrorResult(_lastNodesResult, "Cannot extract service's nodes from Consul response");
            return 0;
        }

        private async Task WaitIfErrorOccuredOnPreviousCall()
        {
            if (Error != null)
            {
                var config = GetConfig();
                var now = DateTime.UtcNow;
                var timeElapsed = ErrorTime - now;
                if (timeElapsed < config.ErrorRetryInterval)
                    await DateTime.Delay(config.ErrorRetryInterval - timeElapsed).ConfigureAwait(false);
            }            
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
            var error = result?.Error ?? new EnvironmentException(errorMessage);

            if (!(error is TaskCanceledException))
                Log.Error("Error calling Consul", exception: result?.Error, unencryptedTags: new
                {
                    ServiceName = ServiceName,
                    ConsulAddress = ConsulClient.ConsulAddress.ToString(),
                    consulQuery = result?.RequestLog,
                    ResponseCode = result?.StatusCode,
                    Content = result?.ResponseContent,
                    ActiveVersion,
                    ConsulVersionRequest = _lastVersionResult?.RequestLog,
                    ConsulVersionResponse = _lastVersionResult?.ResponseContent,
                    ConsulNodesRequest = _lastNodesResult?.RequestLog,
                    ConsulNodesResponse = _lastNodesResult?.ResponseContent,
                });

            Error = error;
            ErrorTime = DateTime.UtcNow;
        }


        private HealthCheckResult CheckHealth()
        {
            var error = _lastNodesResult?.Error ?? _lastVersionResult?.Error ?? Error;
            if (error != null)
            {
                return HealthCheckResult.Unhealthy($"Consul error: " + error.Message);
            }

            var healthMessage = string.Empty;
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

            AggregatingHealthStatus.RemoveCheck(ServiceName);
            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }
    }
}

