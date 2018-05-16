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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Metrics;

using Ninject;
using NSubstitute;
using NUnit.Framework;

using Shouldly;
using Node = Gigya.Microdot.ServiceDiscovery.Rewrite.Node;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class LoadBalancerTests
    {
        private const string ServiceName = "ServiceName";
        private const string Env = "prod";
        private ILoadBalancer _loadBalancer;

        private LogSpy _log;

        private ReachabilityCheck _reachabilityCheck;
        private TestingKernel<LogSpy> _kernel;

        private INodeSource _nodeSource;

        private INode Node1 = new Node("Host1", 111);
        private INode Node2 = new Node("Host2", 222);
        private INode Node3 = new Node("Host3", 333);
        private INode Node4 = new Node("Host4", 444);
        private INode Node5 = new Node("Host5", 555);
        private INode Node6 = new Node("Host6", 666);

        private Func<INode[]> _getSourceNodes = () => new INode[0];

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _kernel = new TestingKernel<LogSpy>();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _kernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {            
            _log = (LogSpy)_kernel.Get<ILog>();
            _nodeSource = Substitute.For<INodeSource>();
            _nodeSource.GetNodes().Returns(_ => _getSourceNodes());
            _reachabilityCheck = (n,c) => throw new EnvironmentException("node is unreachable");

            var createLoadBalancer = _kernel.Get<Func<INodeSource, DeploymentIdentifier, ReachabilityCheck, ILoadBalancer>>();
            _loadBalancer = createLoadBalancer(
                _nodeSource,
                new DeploymentIdentifier(ServiceName, Env),
                (n,c)=>_reachabilityCheck(n,c));
        }

        [TearDown]
        public void TearDown()
        {
            _loadBalancer?.Dispose();
        }

        [Test]
        public void GetNode_ThreeNodes_ReturnsAllThree()
        {
            SetupDefaultNodes();

            var allEndpoints = Get20Nodes();

            new[] { Node1, Node2, Node3 }.ShouldBeSubsetOf(allEndpoints);
        }

        [Test]
        public void GetNode_ThreeNodes_ShouldBeHealthy()
        {
            SetupDefaultNodes();

            _loadBalancer.GetNode();
            var healthStatus = GetHealthStatus();

            healthStatus.IsHealthy.ShouldBeTrue();            
        }

        [Test]
        public void GetNode_NodesChanged_ReturnsNewNodes()
        {
            SetupSourceNodes(Node1,Node2,Node3);
            Get20Nodes();
            SetupSourceNodes(Node4, Node5, Node6);
            

            var res = Get20Nodes();
            res.Distinct()
               .ShouldBe(new[] { Node4, Node5, Node6 }, true);
        }

        [Test]
        public void GetNode_NoNodes_Throws()
        {
            SetupNoNodes();
            Should.Throw<EnvironmentException>(() =>_loadBalancer.GetNode());
        }

        [Test]
        public void GetNode_NodesListBecomesEmpty_Throws()
        {
            SetupDefaultNodes();
            Get20Nodes();
            SetupNoNodes();
            Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode());
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse();            
        }

        [Test]
        public void GetNode_AfterNodeReportedUnreachable_NodeWillNotBeReturned()
        {
            var allNodes = new[] {Node1, Node2, Node3};
            SetupSourceNodes(allNodes);

            var unreachableNode = _loadBalancer.GetNode();
            unreachableNode.ReportUnreachable();

            var nodes = Get20Nodes();
            foreach (var node in allNodes)
            {
                if (node.Equals(unreachableNode))
                    nodes.ShouldNotContain(node);
                else
                    nodes.ShouldContain(node);
            }
        }

        [Test]
        public async Task GetNode_NodeIsReachableAgain_NodeWillBeReturned()
        {            
            SetupDefaultNodes();

            var selectedNode = _loadBalancer.GetNode();
            selectedNode.ReportUnreachable();

            Get20Nodes().ShouldNotContain(selectedNode);

            _reachabilityCheck = (_,__) => Task.FromResult(true);
            await Task.Delay(1000);

            Get20Nodes().ShouldContain(selectedNode);
        }

        [Test]
        public void GetNode_OnlyOneNodeUnreachable_ShouldStillBeHealthy()
        {
            SetupDefaultNodes();        

            Run20times(node =>
            {
                if (node.Equals(Node2))
                    node.ReportUnreachable();
            });

            var healthResult = GetHealthStatus();
            healthResult.IsHealthy.ShouldBeTrue();
            healthResult.Message.ShouldContain(Node2.ToString());
        }


        private void Run20times(Action<IMonitoredNode> act)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var node = _loadBalancer.GetNode();
                    act(node);
                }
                catch
                {
                }
            }
        }

        [Test]
        public async Task GetNode_NodeUnreachableThenReturnsInBackground_NodeShouldBeReturned()
        {
            SetupDefaultNodes();
            _reachabilityCheck = (_,__) => throw new EnvironmentException("node is unreachable");

            var selectedNode = _loadBalancer.GetNode();
            selectedNode.ReportUnreachable();

            Get20Nodes().ShouldNotContain(selectedNode);

            var waitForReachablitiy = new TaskCompletionSource<bool>();
            _reachabilityCheck = (_,__) =>
            {
                waitForReachablitiy.SetResult(true);
                return Task.FromResult(true);
            };
            await waitForReachablitiy.Task;
            await Task.Delay(50);

            Get20Nodes().ShouldContain(selectedNode);
        }

        [Test]
        public void GetNode_AllNodesUnreachable_ThrowsException()
        {
            SetupSourceNodes(Node1,Node2,Node3);

            Run20times(node =>node.ReportUnreachable());

            Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode());
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse(healthStatus.Message);
            healthStatus.Message.ShouldContain("All 3 Nodes are unreachable");
            healthStatus.Message.ShouldContain(Node1.ToString());
            healthStatus.Message.ShouldContain(Node2.ToString());
            healthStatus.Message.ShouldContain(Node3.ToString());
        }

        [Test]
        public async Task GetNode_AllNodesUnreachableThenAllNodesReachable_ReturnsAllNodes()
        {
            SetupSourceNodes(Node1,Node2,Node3);
            Run20times(node => node.ReportUnreachable());
            
            Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode());

            _reachabilityCheck = (_,__) => Task.FromResult(true);

            await Task.Delay(1000);

            var nodes = Get20Nodes();
            nodes.ShouldContain(Node1);
            nodes.ShouldContain(Node2);
            nodes.ShouldContain(Node3);
        }


        [Test]
        public async Task GetNode_NodesUnreachableButReachabilityCheckThrows_ErrorIsLogged()
        {
            SetupDefaultNodes();
            var reachabilityException = new Exception("Simulated error while running reachability check");

            _reachabilityCheck = (_,__) => { throw reachabilityException; };
            Run20times(node => node.ReportUnreachable());

            await Task.Delay(1500);

            _log.LogEntries.ToArray().ShouldContain(e => e.Exception == reachabilityException);
        }

        private void SetupNoNodes()
        {
            SetupSourceNodes( /* no nodes */);
        }

        private void SetupSourceNodes(params INode[] nodes)
        {
            _getSourceNodes = () => nodes;
        }

        private void SetupDefaultNodes()
        {
            SetupSourceNodes(Node1, Node2, Node3);
        }

        INode[] Get20Nodes()
        {
            return Enumerable.Repeat(1, 20).Select(x => _loadBalancer.GetNode()).ToArray();
        }
        private HealthCheckResult GetHealthStatus()
        {
            var healthMonitor = (FakeHealthMonitor)_kernel.Get<IHealthMonitor>();
            return healthMonitor.Monitors[new DeploymentIdentifier(ServiceName,Env).ToString()].Invoke();
        }


    }


}
