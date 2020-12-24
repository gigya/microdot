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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Provides a reachable node for each call to <see cref="TryGetNode"/>
    /// </summary>
    internal sealed class LoadBalancer : ILoadBalancer
    {
        private IDiscovery Discovery { get; }
        private ReachabilityCheck ReachabilityCheck { get; }
        private TrafficRoutingStrategy TrafficRoutingStrategy { get; }
        private Func<Node, DeploymentIdentifier, ReachabilityCheck, Action, NodeMonitoringState> CreateNodeMonitoringState { get; }
        private IDateTime DateTime { get; }
        private ILog Log { get; }
        private DeploymentIdentifier DeploymentIdentifier { get; }
        private Exception LastException { get; set; }
        private Func<DiscoveryConfig> GetConfig { get; }

        private Node[] _lastDiscoveredNodes = new Node[0];
        private Node[] _reachableNodes;
        private NodeMonitoringState[] _nodesMonitoringState = new NodeMonitoringState[0];
        private readonly IDisposable _healthCheck;
        private HealthMessage _healthStatus = new HealthMessage(Health.Info, "Initializing...", suppressMessage: true);
        private DateTime _lastUsageTime;

        int _disposed = 0;
        private readonly object _lock = new object();
        private bool _isUndeployed;



        public LoadBalancer(
            IDiscovery discovery,
            DeploymentIdentifier deploymentIdentifier,
            ReachabilityCheck reachabilityCheck,
            TrafficRoutingStrategy trafficRoutingStrategy,
            Func<Node, DeploymentIdentifier, ReachabilityCheck, Action, NodeMonitoringState> createNodeMonitoringState,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus,
            Func<DiscoveryConfig> getConfig,
            IDateTime dateTime,
            ILog log,
            IEnvironment environment
            )
        {

            DeploymentIdentifier = deploymentIdentifier;
            Discovery = discovery;
            ReachabilityCheck = reachabilityCheck;
            TrafficRoutingStrategy = trafficRoutingStrategy;
            CreateNodeMonitoringState = createNodeMonitoringState;
            GetConfig = getConfig;
            DateTime = dateTime;
            _lastUsageTime = DateTime.UtcNow;
            Log = log;
            var aggregatingHealthStatus = getAggregatingHealthStatus(deploymentIdentifier.ServiceName);
            string healthCheckEntryName = (deploymentIdentifier.DeploymentEnvironment ?? "prod") + deploymentIdentifier.Zone == environment.Zone ? "" : $" ({deploymentIdentifier.Zone})";
            _healthCheck = aggregatingHealthStatus.Register(healthCheckEntryName, CheckHealth);
        }



        public async Task<Node> TryGetNode()
        {
            _lastUsageTime = System.DateTime.UtcNow;
            await LoadNodesFromSource().ConfigureAwait(false);
            if (_isUndeployed)
                return null;

            var nodes = _nodesMonitoringState;
            if (!nodes.Any())
                throw new ServiceUnreachableException("No nodes were discovered for service",
                    LastException,
                    unencrypted: new Tags
                    {
                        {"deploymentIdentifier", DeploymentIdentifier.ToString()},
                    });

            var reachableNodes = _reachableNodes; // get current state of reachable nodes
            if (!reachableNodes.Any())
                throw new ServiceUnreachableException("All nodes are unreachable",
                    nodes.FirstOrDefault(n => n.LastException != null)?.LastException,
                    unencrypted: new Tags
                    {
                        {"deploymentIdentifier", DeploymentIdentifier.ToString()},
                        {"nodes", string.Join(",", nodes.Select(n=>n.Node.ToString()))}
                    });

            var index = GetIndexByTrafficRoutingStrategy();
            return reachableNodes[index % reachableNodes.Length];
        }



        private int _roundRobinIndex = 0;

        private uint GetIndexByTrafficRoutingStrategy()
        {
            switch (TrafficRoutingStrategy)
            {
                case TrafficRoutingStrategy.RoundRobin:
                    return (uint)Interlocked.Increment(ref _roundRobinIndex);
                case TrafficRoutingStrategy.RandomByRequestID:
                    return (uint?)TracingContext.TryGetRequestID()?.GetHashCode() ?? (uint)Interlocked.Increment(ref _roundRobinIndex);
                default:
                    throw new ProgrammaticException($"The {nameof(TrafficRoutingStrategy)} '{TrafficRoutingStrategy}' is not supported by LoadBalancer.");
            }
        }

        private void SetReachableNodes()
        {
            lock (_lock)
            {
                var reachableNodes = _nodesMonitoringState.Where(s => s.IsReachable).Select(s => s.Node).ToArray();
                _reachableNodes = reachableNodes;
                _healthStatus = GetHealthStatus();
            }
        }



        private async Task LoadNodesFromSource()
        {
            if (_disposed > 0)
                return;

            try
            {
                var sourceNodes = await Discovery.GetNodes(DeploymentIdentifier).ConfigureAwait(false);
                if (sourceNodes == _lastDiscoveredNodes)
                    return;

                lock (_lock)
                {
                    if (sourceNodes == null)
                    {
                        _isUndeployed = true;
                        StopMonitoringNodes(_nodesMonitoringState);
                        _nodesMonitoringState = new NodeMonitoringState[0];
                    }
                    else
                    {
                        var newNodes = sourceNodes.Select(CreateState).ToArray();

                        var oldNodes = _nodesMonitoringState;
                        var nodesToRemove = oldNodes.Except(newNodes).ToArray();

                        _nodesMonitoringState = oldNodes.Except(nodesToRemove).Union(newNodes).ToArray();
                        StopMonitoringNodes(nodesToRemove);
                        _isUndeployed = false;

                        foreach (NodeMonitoringState monitoringState in nodesToRemove)
                        {
                            Node node = monitoringState.Node;

                            Log.Info(logDelegate =>
                            {
                                Uri serviceUriHttp = new Uri($"http://{node.Hostname}:{node.Port}");
                                Uri serviceUriHttps = new Uri($"https://{node.Hostname}:{node.Port}");
                                ServicePoint servicePointHttp = ServicePointManager.FindServicePoint(serviceUriHttp);
                                ServicePoint servicePointHttps = ServicePointManager.FindServicePoint(serviceUriHttps);

                                logDelegate.Invoke("Node was removed. See tags for details.",
                                    unencryptedTags: new
                                    {
                                        nodeName = $"{node.Hostname}:{node.Port}",
                                        openedConnectionsTotal = servicePointHttp.CurrentConnections + servicePointHttps.CurrentConnections
                                    });
                            });
                        }

                    }
                    SetReachableNodes();
                    _lastDiscoveredNodes = sourceNodes;
                }
            }
            catch (Exception ex)
            {
                LastException = ex;
            }
        }



        private NodeMonitoringState CreateState(Node node)
        {
            return CreateNodeMonitoringState(node, DeploymentIdentifier, ReachabilityCheck, SetReachableNodes);
        }



        private HealthMessage GetHealthStatus()
        {
            if (_isUndeployed)
                return new HealthMessage(Health.Healthy, message: null, suppressMessage: true); // We don't want a message to show up since the service is not deployed

            if (_nodesMonitoringState.Length == 0)
                return new HealthMessage(Health.Unhealthy, $"No nodes were discovered");

            if (_reachableNodes.Length == _nodesMonitoringState.Length)
                return new HealthMessage(Health.Healthy, $"All {_nodesMonitoringState.Length} nodes are reachable");

            var unreachableNodes = _nodesMonitoringState.Where(n => !_reachableNodes.Contains(n.Node));
            string message = string.Join("\r\n", unreachableNodes.Select(n => $"    {n.Node.ToString()} - {n.LastException?.Message}"));

            if (_reachableNodes.Length == 0)
                return new HealthMessage(Health.Unhealthy, $"All {_nodesMonitoringState.Length} nodes are unreachable\r\n{message}");
            else
                return new HealthMessage(Health.Healthy, $"{_reachableNodes.Length}/{_nodesMonitoringState.Length} nodes are reachable. Unreachable nodes:\r\n{message}");
        }

        private HealthMessage CheckHealth()
        {
            var supressDuration = GetConfig().Services[DeploymentIdentifier.ServiceName].SuppressHealthCheckAfterServiceUnused;
            if (DateTimeOffset.UtcNow.Subtract(_lastUsageTime) > supressDuration)
                return new HealthMessage(Health.Info, message: null, suppressMessage: true); // We don't want a message to show up since the service is not in use

            return _healthStatus;
        }


        private void StopMonitoringNodes(IEnumerable<NodeMonitoringState> monitoredNodes)
        {
            foreach (var monitoredNode in monitoredNodes)
            {
                monitoredNode.StopMonitoring();
            }
        }



        public void ReportUnreachable(Node node, Exception ex)
        {
            var nodeState = _nodesMonitoringState.FirstOrDefault(s => s.Node.Equals(node));
            nodeState?.ReportUnreachable(ex);
        }


        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            StopMonitoringNodes(_nodesMonitoringState);

            _healthCheck.Dispose();
        }


    }
}