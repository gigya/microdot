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
    public class LoadBalancer: ILoadBalancer
    {
        bool _disposed;
        private INodeSource NodeSource { get; }
        private ReachabilityCheck ReachabilityCheck { get; }
        private IDateTime DateTime { get; }
        private ILog Log { get; }
        public string ServiceName { get; }
        private INode[] _sourceNodes;
        private IMonitoredNode[] _monitoredNodes = new IMonitoredNode[0];

        public AggregatingHealthStatus HealthStatus { get; }

        public LoadBalancer(
            INodeSource nodeSource, 
            DeploymentIdentifier deploymentIdentifier, 
            ReachabilityCheck reachabilityCheck,
            Func<string, AggregatingHealthStatus> getAggregatingHealthStatus,
            IDateTime dateTime, 
            ILog log)
        {
            ServiceName = deploymentIdentifier.ToString();
            NodeSource = nodeSource;
            ReachabilityCheck = reachabilityCheck;
            DateTime = dateTime;
            Log = log;
            GetNodesFromSource();
            HealthStatus = getAggregatingHealthStatus("Discovery");
            HealthStatus.RegisterCheck(ServiceName, CheckHealth);
        }

        public IMonitoredNode GetNode()
        {
            GetNodesFromSource();
            var nodes = _monitoredNodes;
            if (nodes.Length==0)
                throw new ServiceUnreachableException("No nodes were discovered for service", unencrypted: new Tags
                {
                    {"serviceName", ServiceName},
                    {"discoverySource", NodeSource.Type}
                });

            var reachableNodes = nodes.Where(n => n.IsReachable).ToArray();
            if (!reachableNodes.Any())
                throw new ServiceUnreachableException("All nodes are unreachable", unencrypted: new Tags
                {
                    {"serviceName", ServiceName},
                    {"discoverySource", NodeSource.Type},
                    {"nodes", string.Join(",", nodes.Select(n=>n.ToString()))}
                });

            var affinityToken = TracingContext.TryGetRequestID() ?? Guid.NewGuid().ToString("N");
            var index = (uint)affinityToken.GetHashCode();
            
            return reachableNodes[index % reachableNodes.Length];
        }

        /// <inheritdoc />
        public bool WasUndeployed => NodeSource.WasUndeployed;

        private void GetNodesFromSource()
        {
            if (_disposed)
                return;

            var sourceNodes = NodeSource.GetNodes();
            if (sourceNodes == _sourceNodes)
                return;

            var oldNodes = _monitoredNodes;
            var newNodes = sourceNodes
                .Select(n => new MonitoredNode(n, ServiceName, ReachabilityCheck, DateTime, Log)).ToArray();
            var nodesToRemove = oldNodes.Except(newNodes).ToArray();

            _monitoredNodes = oldNodes.Except(nodesToRemove).Union(newNodes).ToArray();
            DisposeNodes(nodesToRemove);
            _sourceNodes = sourceNodes;
        }

        private HealthCheckResult CheckHealth()
        {
            if (_monitoredNodes.Length == 0)
                return HealthCheckResult.Unhealthy($"No nodes were discovered by {NodeSource.Type}");

            var unreachableNodes = _monitoredNodes.Where(n => !n.IsReachable).ToArray();
            if (unreachableNodes.Length==0)
                return HealthCheckResult.Healthy($"All {_monitoredNodes.Length} nodes are reachable");

            string message = string.Join("\r\n", unreachableNodes.Select(n=> $"    {n.ToString()} - {n.LastException?.Message}"));
            var healthyNodesCount = _monitoredNodes.Length - unreachableNodes.Length;
            if (healthyNodesCount==0)
                return HealthCheckResult.Unhealthy($"All {_monitoredNodes.Length} nodes are unreachable\r\n{message}");
            else     
                return HealthCheckResult.Healthy($"{healthyNodesCount} nodes out of {_monitoredNodes.Length} are reachable. Unreachable nodes:\r\n{message}");
        }

        private void DisposeNodes(IEnumerable<IMonitoredNode> monitoredNodes)
        {
            foreach (var monitoredNode in monitoredNodes)
            {
                monitoredNode.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            DisposeNodes(_monitoredNodes);
            NodeSource.Dispose();
            _disposed = true;
        }
    }
}