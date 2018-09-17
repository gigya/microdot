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

		private string _lastKnownDeploymentVersion = null;
		private ConsulNode[] _lastKnownNodes = null;

		/// <summary>Contains either the subset of the service nodes that match the current version specified in the KV store, or
		/// an exception describing a Consul issue. Updated atomically. Copy reference before using!</summary>
		private (Node[] Nodes, EnvironmentException LastError) _nodesOrError = (null, null);

		private HealthCheckResult _healthStatus = HealthCheckResult.Healthy();
		private IDisposable _healthCheck;

		private int _stopped = 0;
		private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();

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
			_healthCheck = AggregatingHealthStatus.RegisterCheck(DeploymentIdentifier.ToString(), () => _healthStatus);
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
				// init
				Task<ConsulResponse<string>> deploymentVersionTask = ConsulClient.GetDeploymentVersion(DeploymentIdentifier, InitialModifyIndex, _shutdownToken.Token);
				Task<ConsulResponse<ConsulNode[]>> nodesTask = ConsulClient.GetHealthyNodes(DeploymentIdentifier, InitialModifyIndex, _shutdownToken.Token);
				ConsulResponse<string> deploymentVersion = await deploymentVersionTask.ConfigureAwait(false);
				ConsulResponse<ConsulNode[]> nodes = await nodesTask.ConfigureAwait(false);
				UpdateState(deploymentVersion, nodes);
				_initCompleted.SetResult(true);


				// loop
				while (!_shutdownToken.IsCancellationRequested)
				{
					var winnerTask = await Task.WhenAny(deploymentVersionTask, nodesTask).ConfigureAwait(false);

					if (winnerTask == deploymentVersionTask)
					{
						deploymentVersion = await deploymentVersionTask.ConfigureAwait(false);
						deploymentVersionTask = ConsulClient.GetDeploymentVersion(DeploymentIdentifier,
							deploymentVersion.ModifyIndex ?? InitialModifyIndex, _shutdownToken.Token);
					}
					else if (winnerTask == nodesTask)
					{
						nodes = await nodesTask.ConfigureAwait(false);
						nodesTask = ConsulClient.GetHealthyNodes(DeploymentIdentifier,
							nodes.ModifyIndex ?? InitialModifyIndex, _shutdownToken.Token);
					}

					UpdateState(deploymentVersion, nodes);
					// Add a delay before calling Consul again if there was some error, so we don't spam it
					if (deploymentVersion.Error != null || nodes.Error != null)
						await DateTime.DelayUntil(DateTime.UtcNow + GetConfig().ErrorRetryInterval, _shutdownToken.Token).ConfigureAwait(false);
				}
			}
			catch (TaskCanceledException) when (_shutdownToken.IsCancellationRequested)
			{
				// Ignore exception during shutdown.
			}
			catch (Exception ex)
			{
				_nodesOrError = (null, new EnvironmentException("Error calling Consul", ex, unencrypted: new Tags
																		{
																			{ "serviceName", DeploymentIdentifier.ServiceName },
																			{ "serviceEnv",  DeploymentIdentifier.DeploymentEnvironment}
																		}));
				_healthStatus = HealthCheckResult.Unhealthy($"Consul error: {ex.Message}");
			}
			finally
			{
				_initCompleted.TrySetResult(true);
			}		
		}


		private void UpdateState(ConsulResponse<string> versionResponse, ConsulResponse<ConsulNode[]> nodesResponse)
		{
			if (versionResponse.Error == null && versionResponse.IsUndeployed == false)
				_lastKnownDeploymentVersion = versionResponse.Response;
			if (nodesResponse.Error == null)
				_lastKnownNodes = nodesResponse.Response;

			// Service no longer deployed
			if (versionResponse.IsUndeployed == true)
			{
				Log.Info(_ => _("Consul reported service was undeployed", unencryptedTags: MakeLogTags(versionResponse, nodesResponse)));
				_healthStatus = HealthCheckResult.Healthy("Service was undeployed");
			}

			// Error getting version or nodes
			else if (_lastKnownDeploymentVersion == null || _lastKnownNodes == null)
			{
				Log.Error(_ => _("Consul error", exception: versionResponse.Error ?? nodesResponse.Error, unencryptedTags: MakeLogTags(versionResponse, nodesResponse)));
				_healthStatus = HealthCheckResult.Unhealthy("Consul error: " + (versionResponse.Error ?? nodesResponse.Error).Message);
				_nodesOrError = (null, new EnvironmentException("Consul error", versionResponse.Error ?? nodesResponse.Error, unencrypted: MakeExceptionTags(versionResponse, nodesResponse)));
			}

			// No healthy nodes with matching version
			else if (_lastKnownNodes.All(n => n.Version != _lastKnownDeploymentVersion))
			{
				Log.Error(_ => _("No nodes were specified in Consul for the requested service and service's active version.", unencryptedTags: MakeLogTags(versionResponse, nodesResponse)));
				_healthStatus = HealthCheckResult.Unhealthy("No nodes were specified in Consul for the requested service and service's active version.");
				_nodesOrError = (null, new EnvironmentException("No nodes were specified in Consul for the requested service and service's active version.", unencrypted: MakeExceptionTags(versionResponse, nodesResponse)));
			}

			// All ok, update nodes
			else
			{
				var nodes = _lastKnownNodes.Where(n => n.Version == _lastKnownDeploymentVersion).Select(_ => new Node(_.Hostname, _.Port)).ToArray();
				_nodesOrError = (nodes, null);
				if (versionResponse.Error != null || nodesResponse.Error != null)
					_healthStatus = HealthCheckResult.Unhealthy("Consul error: " + (versionResponse.Error ?? nodesResponse.Error).Message);
				else if (nodes.Length < _lastKnownNodes.Length)
					_healthStatus = HealthCheckResult.Healthy($"{nodes.Length} nodes matching to version {_lastKnownDeploymentVersion} from total of {_lastKnownNodes.Length} nodes");
				else _healthStatus = HealthCheckResult.Healthy($"{nodes.Length} nodes");
			}
		}



		object MakeLogTags(ConsulResponse<string> deploymentResponse, ConsulResponse<ConsulNode[]> nodesResponse) => new
		{
			serviceName = DeploymentIdentifier.ServiceName,
			serviceEnv = DeploymentIdentifier.DeploymentEnvironment,

			consulAddress = deploymentResponse.ConsulAddress ?? nodesResponse.ConsulAddress,
			activeVersion = deploymentResponse.Response,

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
			{"serviceZone",             DeploymentIdentifier.Zone},

			{"consulAddress",           deploymentResponse.ConsulAddress ?? nodesResponse.ConsulAddress},
			{"activeVersion",           deploymentResponse.Response},

			{"lastVersionResponseCode", deploymentResponse.StatusCode.ToString()},
			{"lastVersionCommand",      deploymentResponse.CommandPath},
			{"lastVersionResponse",     deploymentResponse.ResponseContent},

			{"lastNodesResponseCode",   nodesResponse.StatusCode.ToString()},
			{"lastNodesCommand",        nodesResponse.CommandPath},
			{"lastNodesResponse",       nodesResponse.ResponseContent}
		};

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
