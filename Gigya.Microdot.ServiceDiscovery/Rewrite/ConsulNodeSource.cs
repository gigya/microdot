#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

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
	internal class ConsulNodeSource : INodeSource
	{
		private const int InitialModifyIndex = 0;
		private ILog Log { get; }
		private ConsulClient ConsulClient { get; }
		private IDateTime DateTime { get; }
		private Func<ConsulConfig> GetConfig { get; }
		private AggregatingHealthStatus AggregatingHealthStatus { get; }
		private DeploymentIdentifier DeploymentIdentifier { get; }

		/// <summary>Contains either the subset of the service nodes that match the current version specified in the KV store, or
		/// an exception describing a Consul issue. Updated atomically. Copy reference before using!</summary>
		private (Node[] Nodes, EnvironmentException LastError) _nodesOrError = (null, null);

		private HealthMessage _healthStatus = new HealthMessage(Health.Info, "Initializing...", suppressMessage: true);
        private readonly IDisposable _healthCheck;

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
			AggregatingHealthStatus = getAggregatingHealthStatus("Consul");
			_healthCheck = AggregatingHealthStatus.Register(DeploymentIdentifier.ToString(), () => _healthStatus);
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




		private async Task UpdateLoop()
		{
			try
			{
				Task<ConsulResponse<string>> deploymentVersionTask = ConsulClient.GetDeploymentVersion(DeploymentIdentifier, InitialModifyIndex, _shutdownToken.Token);
				Task<ConsulResponse<ConsulNode[]>> healthyNodesTask = ConsulClient.GetHealthyNodes(DeploymentIdentifier, InitialModifyIndex, _shutdownToken.Token);
				await Task.WhenAll(deploymentVersionTask, healthyNodesTask).ConfigureAwait(false);

				ConsulResponse<string> deploymentVersion = null;
				ConsulResponse<ConsulNode[]> healthyNodes = null;

				string lastKnownDeploymentVersion = null;
				ConsulNode[] lastKnownNodes = null;

				while (!_shutdownToken.IsCancellationRequested)
				{
					if (deploymentVersionTask.IsCompleted)
					{
						deploymentVersion = deploymentVersionTask.Result;
						if (deploymentVersion.Error == null && deploymentVersion.IsUndeployed == false)
							lastKnownDeploymentVersion = deploymentVersion.ResponseObject;
						deploymentVersionTask = ConsulClient.GetDeploymentVersion(DeploymentIdentifier, deploymentVersion.ModifyIndex ?? InitialModifyIndex, _shutdownToken.Token);
					}

					if (healthyNodesTask.IsCompleted)
					{
						healthyNodes = healthyNodesTask.Result;
						if (healthyNodes.Error == null)
							lastKnownNodes = healthyNodes.ResponseObject;
						healthyNodesTask = ConsulClient.GetHealthyNodes(DeploymentIdentifier, healthyNodes.ModifyIndex ?? InitialModifyIndex, _shutdownToken.Token);
					}

					Update(deploymentVersion, healthyNodes, lastKnownDeploymentVersion, lastKnownNodes);
					_initCompleted.TrySetResult(true);

					// Add a delay before calling Consul again if there was some error, so we don't spam it
					if (deploymentVersion?.ModifyIndex == null || healthyNodes?.ModifyIndex == null)
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



		private void Update(ConsulResponse<string> deploymentVersionResponse, ConsulResponse<ConsulNode[]> nodesResponse,
			string lastKnownDeploymentVersion = null, ConsulNode[] lastKnownNodes = null)
		{
			// Service no longer deployed
			if (deploymentVersionResponse.IsUndeployed == true)
			{
				Log.Info(_ => _("Consul reported service was undeployed", unencryptedTags: MakeLogTags(deploymentVersionResponse, nodesResponse)));
				_healthStatus = new HealthMessage(Health.Healthy, "Service was undeployed");
			}

			// Error getting version or nodes
			else if (lastKnownDeploymentVersion == null || lastKnownNodes == null)
			{
				Log.Error(_ => _("Consul error", exception: deploymentVersionResponse.Error ?? nodesResponse.Error, unencryptedTags: MakeLogTags(deploymentVersionResponse, nodesResponse)));
				_healthStatus = new HealthMessage(Health.Unhealthy, "Consul error: " + (deploymentVersionResponse.Error ?? nodesResponse.Error).Message);
				_nodesOrError = (null, new EnvironmentException("Consul error", deploymentVersionResponse.Error ?? nodesResponse.Error, unencrypted: MakeExceptionTags(deploymentVersionResponse, nodesResponse)));
			}

			// No healthy nodes with matching version
			else if (lastKnownNodes.All(n => n.Version != lastKnownDeploymentVersion))
			{
				Log.Error(_ => _("No nodes were specified in Consul for the requested service and service's active version.", unencryptedTags: MakeLogTags(deploymentVersionResponse, nodesResponse)));
				_healthStatus = new HealthMessage(Health.Unhealthy, "No nodes were specified in Consul for the requested service and service's active version.");
				_nodesOrError = (null, new EnvironmentException("No nodes were specified in Consul for the requested service and service's active version.", unencrypted: MakeExceptionTags(deploymentVersionResponse, nodesResponse)));
			}

			// All ok, update nodes
			else
			{
				var nodes = lastKnownNodes.Where(n => n.Version == lastKnownDeploymentVersion).Select(_ => new Node(_.Hostname, _.Port)).ToArray();
				_nodesOrError = (nodes, null);
				if (deploymentVersionResponse.Error != null || nodesResponse.Error != null)
					_healthStatus = new HealthMessage(Health.Unhealthy, "Consul error: " + (deploymentVersionResponse.Error ?? nodesResponse.Error).Message);
				else if (nodes.Length < lastKnownNodes.Length)
					_healthStatus = new HealthMessage(Health.Healthy, $"{nodes.Length} nodes matching to version {lastKnownDeploymentVersion} from total of {lastKnownNodes.Length} nodes");
				else _healthStatus = new HealthMessage(Health.Healthy, $"{nodes.Length} nodes");
			}
		}



		object MakeLogTags(ConsulResponse<string> deploymentResponse, ConsulResponse<ConsulNode[]> nodesResponse) => new
		{
			serviceName = DeploymentIdentifier.ServiceName,
			serviceEnv = DeploymentIdentifier.DeploymentEnvironment,

			consulAddress = deploymentResponse.ConsulAddress ?? nodesResponse.ConsulAddress,
			activeVersion = deploymentResponse.ResponseObject,

			lastVersionResponseCode = deploymentResponse.StatusCode,
			lastVersionCommand = deploymentResponse.CommandPath,
			lastVersionResponse = deploymentResponse.ResponseContent,

			lastNodesResponseCode = nodesResponse.StatusCode,
			lastNodesCommand = nodesResponse.CommandPath,
			lastNodesResponse = nodesResponse.ResponseContent,
		};



		Tags MakeExceptionTags(ConsulResponse<string> deploymentResponse, ConsulResponse<ConsulNode[]> nodesResponse) => new Tags
		{
			{"serviceName",             DeploymentIdentifier.ServiceName},
			{"serviceEnv",              DeploymentIdentifier.DeploymentEnvironment},

			{"consulAddress",           deploymentResponse.ConsulAddress ?? nodesResponse.ConsulAddress},
			{"activeVersion",           deploymentResponse.ResponseObject},

			{"lastVersionResponseCode", deploymentResponse.StatusCode.ToString()},
			{"lastVersionCommand",      deploymentResponse.CommandPath},
			{"lastVersionResponse",     deploymentResponse.ResponseContent},

			{"lastNodesResponseCode",   nodesResponse.StatusCode.ToString()},
			{"lastNodesCommand",        nodesResponse.CommandPath},
			{"lastNodesResponse",       nodesResponse.ResponseContent}
		};



		private int _stopped = 0;
		private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();

		public void Dispose()
		{
			if (Interlocked.Increment(ref _stopped) != 1)
				return;

			_healthCheck.Dispose();

			_shutdownToken.Cancel();
			_shutdownToken.Dispose();
		}
	}
}
