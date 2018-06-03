using System;
using System.Linq;
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

    /// <summary>
    /// Monitors Consul using Health API and KeyValue API to find the current active version of a service,
    /// and provides a list of up-to-date, healthy nodes.
    /// </summary>
    internal class ConsulNodeSource: INodeSource, IDisposable
    {
        private ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private AggregatingHealthStatus AggregatingHealthStatus { get; }
        private DeploymentIdentifier DeploymentIdentifier { get; }

        /// <summary>Contains either the subset of the service nodes that match the current version specified in the KV store, or
        /// an exception describing a Consul issue. Updated atomically. Copy reference before using!</summary>
        private (Node[] Nodes, EnvironmentException LastError) _nodesOrError = (null, null);

        private HealthCheckResult _healthStatus = HealthCheckResult.Healthy();

        public ConsulNodeSource(
            DeploymentIdentifier deploymentIdentifier,
            ILog log,            
            ConsulClient consulClient,
            IDateTime dateTime,
            Func<ConsulConfig> getConfig,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus)
        {
            DeploymentIdentifier = deploymentIdentifier;            
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            AggregatingHealthStatus = getAggregatingHealthStatus("ConsulClient"); // TODO: rename to "Consul"
            AggregatingHealthStatus.RegisterCheck(DeploymentIdentifier.ToString(), () => _healthStatus);
            Task.Run(UpdateLoop); // So that the loop doesn't run on a Grain task scheduler
        }


        /// <inheritdoc />
        public Task Init() => _initCompleted.Task;
        private readonly TaskCompletionSource<bool> _initCompleted = new TaskCompletionSource<bool>();


        /// <inheritdoc />
        public Node[] GetNodes()
        {
            var nodes = _nodesOrError;
            if (nodes.LastError != null)
                throw nodes.LastError;
            else return nodes.Nodes;
        }


        /// <inheritdoc />
        public bool WasUndeployed { get; set; }



        private async Task UpdateLoop()
        {
            try
            {
                Task<ConsulResponse<string>> deploymentVersionTask = ConsulClient.GetDeploymentVersion(DeploymentIdentifier, 0, _shutdownToken.Token);
                Task<ConsulResponse<ConsulNode[]>> healthyNodesTask = ConsulClient.GetHealthyNodes(DeploymentIdentifier, 0, _shutdownToken.Token);
                await Task.WhenAll(deploymentVersionTask, healthyNodesTask).ConfigureAwait(false);

                ConsulResponse<string> deploymentVersion = null;
                ConsulResponse<ConsulNode[]> healthyNodes = null;

                while (!_shutdownToken.IsCancellationRequested)
                {
                    if (deploymentVersionTask.IsCompleted)
                    {
                        deploymentVersion = deploymentVersionTask.Result;
                        deploymentVersionTask = ConsulClient.GetDeploymentVersion(DeploymentIdentifier, deploymentVersionTask.Result.ModifyIndex ?? 0, _shutdownToken.Token);
                    }

                    if (healthyNodesTask.IsCompleted)
                    {
                        healthyNodes = healthyNodesTask.Result;
                        healthyNodesTask = ConsulClient.GetHealthyNodes(DeploymentIdentifier, healthyNodesTask.Result.ModifyIndex ?? 0, _shutdownToken.Token);
                    }

                    Update(deploymentVersion, healthyNodes);
                    _initCompleted.TrySetResult(true);

                    // Add a delay before calling Consul again if there was some error, so we don't spam it
                    if (_nodesOrError.LastError != null)
                        await DateTime.DelayUntil(DateTime.UtcNow + GetConfig().ErrorRetryInterval, _shutdownToken.Token).ConfigureAwait(false);

                    await Task.WhenAny(deploymentVersionTask, healthyNodesTask).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (_shutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
            finally
            {
                _initCompleted.TrySetResult(true);
            }
        }



        private void Update(ConsulResponse<string> deploymentVersion, ConsulResponse<ConsulNode[]> healthyNodes)
        {
            // Service no longer deployed
            if (deploymentVersion.IsUndeployed == true)
            {
                Log.Info(_ => _("Consul reported service was undeployed", unencryptedTags: MakeLogTags(deploymentVersion, healthyNodes)));
                _healthStatus = HealthCheckResult.Healthy("Service was undeployed");
                WasUndeployed = true;
                _shutdownToken.Cancel(); // stop polling Consul
            }

            // Error getting version or nodes
            else if (deploymentVersion.Error != null || healthyNodes.Error != null)
            {
                Log.Error(_ => _("Consul error", exception: deploymentVersion.Error ?? healthyNodes.Error, unencryptedTags: MakeLogTags(deploymentVersion, healthyNodes)));
                _healthStatus = HealthCheckResult.Unhealthy("Consul error: " + (deploymentVersion.Error ?? healthyNodes.Error).Message);
                if (_nodesOrError.Nodes == null) // update error, only if we haven't got nodes
                    _nodesOrError = (null, new EnvironmentException("Consul error", deploymentVersion.Error ?? healthyNodes.Error, unencrypted: MakeExceptionTags(deploymentVersion, healthyNodes)));
            }

            // No healthy nodes with matching version
            else if (healthyNodes.Result.All(n => n.Version != deploymentVersion.Result))
            {
                Log.Error(_ => _("No nodes were specified in Consul for the requested service and service's active version.", unencryptedTags: MakeLogTags(deploymentVersion, healthyNodes)));
                _healthStatus = HealthCheckResult.Unhealthy("No nodes were specified in Consul for the requested service and service's active version.");
                if (_nodesOrError.Nodes == null) // update error, only if we haven't got nodes
                    _nodesOrError = (null, new EnvironmentException("No nodes were specified in Consul for the requested service and service's active version.", unencrypted: MakeExceptionTags(deploymentVersion, healthyNodes)));
            }

            // All ok, update nodes
            else
            {
                var nodes = healthyNodes.Result.Where(n => n.Version == deploymentVersion.Result).Select(_ => new Node(_.Hostname, _.Port)).ToArray();
                if (nodes.Length < healthyNodes.Result.Length)
                    _healthStatus = HealthCheckResult.Healthy($"{nodes.Length} nodes matching to version {deploymentVersion.Result} from total of {healthyNodes.Result.Length} nodes");
                else _healthStatus = HealthCheckResult.Healthy($"{nodes.Length} nodes");
                _nodesOrError = (nodes, null);
            }
        }



        object MakeLogTags(ConsulResponse<string> deploymentResponse, ConsulResponse<ConsulNode[]> nodesResponse) => new
        {
            serviceName             = DeploymentIdentifier.ServiceName,
            serviceEnv              = DeploymentIdentifier.DeploymentEnvironment,

            consulAddress           = deploymentResponse.ConsulAddress ?? nodesResponse.ConsulAddress,
            activeVersion           = deploymentResponse.Result,

            lastVersionResponseCode = deploymentResponse.StatusCode,
            lastVersionCommand      = deploymentResponse.CommandPath,
            lastVersionResponse     = deploymentResponse.ResponseContent,

            lastNodesResponseCode   = nodesResponse.StatusCode,
            lastNodesCommand        = nodesResponse.CommandPath,
            lastNodesResponse       = nodesResponse.ResponseContent,
        };



        Tags MakeExceptionTags(ConsulResponse<string> deploymentResponse, ConsulResponse<ConsulNode[]> nodesResponse) => new Tags
        {
            {"serviceName",             DeploymentIdentifier.ServiceName},
            {"serviceEnv",              DeploymentIdentifier.DeploymentEnvironment},

            {"consulAddress",           deploymentResponse?.ConsulAddress},
            {"activeVersion",           deploymentResponse?.Result},

            {"lastVersionResponseCode", deploymentResponse?.StatusCode.ToString()},
            {"lastVersionCommand",      deploymentResponse?.CommandPath},
            {"lastVersionResponse",     deploymentResponse?.ResponseContent},

            {"lastNodesResponseCode",   nodesResponse?.StatusCode.ToString()},
            {"lastNodesCommand",        nodesResponse?.CommandPath},
            {"lastNodesResponse",       nodesResponse?.ResponseContent}
        };



        private int _stopped = 0;
        private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();

        public void Dispose()
        {
            if (Interlocked.Increment(ref _stopped) != 1)
                return;

            AggregatingHealthStatus.RemoveCheck(DeploymentIdentifier.ToString()); // TODO: handle case where another instance might have registered with that ID; don't unregister it; use a handle?

            _shutdownToken.Cancel();
            _shutdownToken.Dispose();
        }

        public string Type => "Consul";
    }
}
