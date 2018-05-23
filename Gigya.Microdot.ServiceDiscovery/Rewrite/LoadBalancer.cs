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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Provides a reachable node for each call to <see cref="GetNode"/>
    /// </summary>
    internal sealed class LoadBalancer: ILoadBalancer
    {
        int _disposed = 0;
        private readonly object _lock = new object();

        private ReachabilityCheck ReachabilityCheck { get; }
        private Func<Node, string, ReachabilityCheck, Action, NodeMonitoringState> CreateNodeMonitoringState { get; }
        private IDateTime DateTime { get; }
        private ILog Log { get; }
        private string DeploymentIdentifier { get; }
        private EnvironmentException LastException { get; set; }

        private Node[] _sourceNodes;
        private Node[] _reachableNodes;
        private NodeMonitoringState[] _nodesState = new NodeMonitoringState[0];
        private readonly ComponentHealthMonitor _healthMonitor;        

        public LoadBalancer(
            INodeSource nodeSource, 
            DeploymentIdentifier deploymentIdentifier, 
            ReachabilityCheck reachabilityCheck,
            Func<Node, string, ReachabilityCheck, Action, NodeMonitoringState> createNodeMonitoringState,
            IHealthMonitor healthMonitor,
            IDateTime dateTime, 
            ILog log)
        {
            DeploymentIdentifier = deploymentIdentifier.ToString();
            NodeSource = nodeSource;
            ReachabilityCheck = reachabilityCheck;
            CreateNodeMonitoringState = createNodeMonitoringState;
            DateTime = dateTime;
            Log = log;
            GetNodesFromSource();
            _healthMonitor = healthMonitor.SetHealthFunction(DeploymentIdentifier, CheckHealth);
        }

        /// <inheritdoc />
        public INodeSource NodeSource { get; }

        public Node GetNode()
        {
            GetNodesFromSource();
            var nodes = _nodesState;
            if (!nodes.Any())
                throw new ServiceUnreachableException("No nodes were discovered for service", unencrypted: new Tags
                {
                    {"deploymentIdentifier", DeploymentIdentifier},
                    {"nodeSource", NodeSource.GetType().Name}
                });

            var reachableNodes = _reachableNodes; // get current state of reachable nodes
            if (!reachableNodes.Any())
                throw new ServiceUnreachableException("All nodes are unreachable",
                    nodes.FirstOrDefault(n=>n.LastException!=null)?.LastException,
                    unencrypted: new Tags
                    {
                        {"deploymentIdentifier", DeploymentIdentifier},
                        {"nodeSource", NodeSource.GetType().Name},
                        {"nodes", string.Join(",", nodes.Select(n=>n.ToString()))}                    
                    });
            
            var affinityToken = TracingContext.TryGetRequestID() ?? Guid.NewGuid().ToString("N");
            var index = (uint)affinityToken.GetHashCode();
            
            return reachableNodes[index % reachableNodes.Length];
        }

        private void SetReachableNodes()
        {
            _reachableNodes = _nodesState.Where(s => s.IsReachable).Select(s=>s.Node).ToArray();
        }

        private void GetNodesFromSource()
        {
            if (_disposed>0)
                return;

            try
            {
                var sourceNodes = NodeSource.GetNodes();
                if (sourceNodes == _sourceNodes)
                    return;

                var newNodes = sourceNodes.Select(CreateState).ToArray();

                lock (_lock)
                {
                    var oldNodes = _nodesState;
                    var nodesToRemove = oldNodes.Except(newNodes).ToArray();

                    _nodesState = oldNodes.Except(nodesToRemove).Union(newNodes).ToArray();
                    StopMonitoringNodes(nodesToRemove);
                    _sourceNodes = sourceNodes;
                    SetReachableNodes();
                }
            }
            catch (EnvironmentException ex)
            {
                LastException = ex;
            }
        }

        private NodeMonitoringState CreateState(Node node)
        {
            return CreateNodeMonitoringState(node, DeploymentIdentifier, ReachabilityCheck, SetReachableNodes);
        }

        private HealthCheckResult CheckHealth()
        {
            var nodes = _nodesState;
            if (nodes.Length == 0)
                return HealthCheckResult.Unhealthy($"No nodes were discovered by {NodeSource.GetType().Name}");

            var unreachableNodes = nodes.Where(n => !n.IsReachable).ToArray();
            if (unreachableNodes.Length==0)
                return HealthCheckResult.Healthy($"All {nodes.Length} nodes are reachable");

            string message = string.Join("\r\n", unreachableNodes.Select(n=> $"    {n.Node.ToString()} - {n.LastException?.Message}"));
            var healthyNodesCount = nodes.Length - unreachableNodes.Length;
            if (healthyNodesCount==0)
                return HealthCheckResult.Unhealthy($"All {nodes.Length} nodes are unreachable\r\n{message}");
            else     
                return HealthCheckResult.Healthy($"{healthyNodesCount} nodes out of {nodes.Length} are reachable. Unreachable nodes:\r\n{message}");
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
            var nodeState = _nodesState.FirstOrDefault(s => s.Node.Equals(node));
            nodeState?.ReportUnreachable(ex);
        }


        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            StopMonitoringNodes(_nodesState);
            NodeSource.Shutdown();
            _healthMonitor.Dispose();            
        }


    }
}