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
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Metrics;

using Ninject;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class LoadBalancerTests
    {
        private const int Repeat = 3;
        private const string ServiceName = "ServiceName";
        private const string Env = "prod";
        private ILoadBalancer _loadBalancer;

        private LogSpy _log;

        private ReachabilityCheck _reachabilityCheck;
        private TestingKernel<LogSpy> _kernel;

        private IDiscovery _discovery;

        private readonly Node _node1 = new Node("Host1", 111);
        private readonly Node _node2 = new Node("Host2", 222);
        private readonly Node _node3 = new Node("Host3", 333);
        private readonly Node _node4 = new Node("Host4", 444);
        private readonly Node _node5 = new Node("Host5", 555);
        private readonly Node _node6 = new Node("Host6", 666);

        private Func<Node[]> _getSourceNodes = () => new Node[0];
        private IEnvironment _environment;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _kernel = new TestingKernel<LogSpy>(k => k.Rebind<IDiscovery>().ToMethod(_ => _discovery));
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
            _discovery = Substitute.For<IDiscovery>();
            _discovery.GetNodes(Arg.Any<DeploymentIdentifier>()).Returns(_ => Task.FromResult(_getSourceNodes()));
            _reachabilityCheck = (n, c) => throw new EnvironmentException("node is unreachable");
            _environment = Substitute.For<IEnvironment>();
        }

        private void CreateLoadBalancer(TrafficRoutingStrategy trafficRoutingStrategy = TrafficRoutingStrategy.RandomByRequestID)
        {
            var createLoadBalancer = _kernel.Get<Func<DeploymentIdentifier, ReachabilityCheck, TrafficRoutingStrategy, ILoadBalancer>>();
            _loadBalancer = createLoadBalancer(
                new DeploymentIdentifier(ServiceName, Env, _environment),
                (n, c) => _reachabilityCheck(n, c),
                trafficRoutingStrategy);
        }

        [TearDown]
        public void TearDown()
        {
            _loadBalancer?.Dispose();
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_RoutingTrafficRoundRobin_GetDifferent3NodesAfterExactly3Times()
        {
            CreateLoadBalancer(TrafficRoutingStrategy.RoundRobin);
            SetupDefaultNodes();
            var allEndpoints = await GetNodes(3);
            allEndpoints.ShouldContain(_node1);
            allEndpoints.ShouldContain(_node2);
            allEndpoints.ShouldContain(_node3);
        }

        [Test]
        public async Task GetNode_ThreeNodes_ReturnsAllThree()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();

            var allEndpoints = await Get20Nodes();

            new[] { _node1, _node2, _node3 }.ShouldBeSubsetOf(allEndpoints);
        }

        [Test]
        public void GetNode_ThreeNodes_ShouldBeHealthy()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();

            _loadBalancer.TryGetNode();
            var healthStatus = GetHealthStatus();

            healthStatus.IsHealthy.ShouldBeTrue();
        }

        [Test]
        public async Task GetNode_ClonedNodes_ReturnsDistinctNodes()
        {
            Node node1Clone = new Node("Host1", 111);

            CreateLoadBalancer();
            SetupSourceNodes(_node1, node1Clone);

            await _loadBalancer.TryGetNode();
            var healthStatus = GetHealthStatus();


            healthStatus.Message.ShouldBe("[OK]  (): All 1 nodes are reachable");
        }

        [Test]
        public async Task GetNode_NodesChanged_ReturnsNewNodes()
        {
            CreateLoadBalancer();
            SetupSourceNodes(_node1, _node2, _node3);
            Get20Nodes();
            SetupSourceNodes(_node4, _node5, _node6);


            var res = await Get20Nodes();
            res.Distinct()
                .ShouldBe(new[] { _node4, _node5, _node6 }, true);
        }

        [Test]
        public void GetNode_NoNodes_Throws()
        {
            CreateLoadBalancer();
            SetupNoNodes();
            Should.Throw<EnvironmentException>(() => _loadBalancer.TryGetNode());
        }

        [Test]
        [Repeat(Repeat)]
        public void GetNode_NodesListBecomesEmpty_Throws()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();
            Get20Nodes();
            SetupNoNodes();
            Should.Throw<EnvironmentException>(() => _loadBalancer.TryGetNode());
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse();
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_AfterNodeReportedUnreachable_NodeWillNotBeReturned()
        {
            CreateLoadBalancer();
            var allNodes = new[] { _node1, _node2, _node3 };
            SetupSourceNodes(allNodes);

            var unreachableNode = await _loadBalancer.TryGetNode();
            _loadBalancer.ReportUnreachable(unreachableNode);

            var nodes = await Get20Nodes();
            foreach (var node in allNodes)
            {
                if (node.Equals(unreachableNode))
                    nodes.ShouldNotContain(node);
                else
                    nodes.ShouldContain(node);
            }
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_TwoNodesUnreachable_OneBecomesReachable_ReturnOnlyReachableNode()
        {
            Node nodeToBeUnreachable = null;

            _reachabilityCheck = async (n, c) =>
            {
                // ReSharper disable once AccessToModifiedClosure
                if (Equals(n, nodeToBeUnreachable)) throw new Exception("This node is still unreachable");
            };
            CreateLoadBalancer();

            var allNodes = new[] { _node1, _node2, _node3 };
            SetupSourceNodes(allNodes);

            nodeToBeUnreachable = await _loadBalancer.TryGetNode();
            var nodeToBeReachable = await GetDifferentNode(nodeToBeUnreachable);

            _loadBalancer.ReportUnreachable(nodeToBeReachable);
            _loadBalancer.ReportUnreachable(nodeToBeUnreachable);

            await Task.Delay(1000);

            var nodes = await Get20Nodes();
            foreach (var node in allNodes)
            {
                if (node.Equals(nodeToBeUnreachable))
                    nodes.ShouldNotContain(node);
                else
                    nodes.ShouldContain(node);
            }
        }

        private async Task<Node> GetDifferentNode(Node nodeToCompare)
        {
            var differentNode = nodeToCompare;
            while (Equals(differentNode, nodeToCompare))
                differentNode = await _loadBalancer.TryGetNode();

            return differentNode;
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_NodeIsReachableAgain_NodeWillBeReturned()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();

            var selectedNode = await _loadBalancer.TryGetNode();
            _loadBalancer.ReportUnreachable(selectedNode);

            (await Get20Nodes()).ShouldNotContain(selectedNode);

            _reachabilityCheck = (_, __) => Task.FromResult(true);
            await Task.Delay(1000);

            (await Get20Nodes()).ShouldContain(selectedNode);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_OnlyOneNodeUnreachable_ShouldStillBeHealthy()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();

            await Run20Times(node =>
            {
                if (node.Equals(_node2))
                    _loadBalancer.ReportUnreachable(node);
            });

            var healthResult = GetHealthStatus();
            healthResult.IsHealthy.ShouldBeTrue();
            healthResult.Message.ShouldContain(_node2.ToString());
        }


        private async Task Run20Times(Action<Node> act)
        {
            for (var i = 0; i < 20; i++)
            {
                try
                {
                    var node = await _loadBalancer.TryGetNode();
                    act(node);
                }
                catch
                {
                    // ignored
                }
            }
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_NodeUnreachableThenReturnsInBackground_NodeShouldBeReturned()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();
            _reachabilityCheck = (_, __) => throw new EnvironmentException("node is unreachable");

            var selectedNode = await _loadBalancer.TryGetNode();
            _loadBalancer.ReportUnreachable(selectedNode);

            (await Get20Nodes()).ShouldNotContain(selectedNode);

            var waitForReachablitiy = new TaskCompletionSource<bool>();
            _reachabilityCheck = (_, __) =>
            {
                waitForReachablitiy.SetResult(true);
                return Task.FromResult(true);
            };
            await waitForReachablitiy.Task;
            await Task.Delay(50);

            (await Get20Nodes()).ShouldContain(selectedNode);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_AllNodesUnreachable_ThrowsException()
        {
            CreateLoadBalancer();
            SetupSourceNodes(_node1, _node2, _node3);

            await Run20Times(node => _loadBalancer.ReportUnreachable(node));

            Should.Throw<EnvironmentException>(() => _loadBalancer.TryGetNode());
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse(healthStatus.Message);
            healthStatus.Message.ShouldContain("All 3 Nodes are unreachable");
            healthStatus.Message.ShouldContain(_node1.ToString());
            healthStatus.Message.ShouldContain(_node2.ToString());
            healthStatus.Message.ShouldContain(_node3.ToString());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_AllNodesUnreachableThenAllNodesReachable_ReturnsAllNodes()
        {
            CreateLoadBalancer();
            SetupSourceNodes(_node1, _node2, _node3);
            await Run20Times(node => _loadBalancer.ReportUnreachable(node));

            Should.Throw<EnvironmentException>(() => _loadBalancer.TryGetNode());

            _reachabilityCheck = (_, __) => Task.FromResult(true);

            await Task.Delay(1000);

            var nodes = await Get20Nodes();
            nodes.ShouldContain(_node1);
            nodes.ShouldContain(_node2);
            nodes.ShouldContain(_node3);
        }


        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_NodesUnreachableButReachabilityCheckThrows_ErrorIsLogged()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();
            var reachabilityException = new Exception("Simulated error while running reachability check");

            _reachabilityCheck = (_, __) => throw reachabilityException;
            await Run20Times(node => _loadBalancer.ReportUnreachable(node));

            await Task.Delay(1500);

            _log.LogEntries.ToArray().ShouldContain(e => e.Exception == reachabilityException);
        }

        [Test]
        public async Task ErrorGettingNodes_MatchingExceptionIsThrown()
        {
            CreateLoadBalancer();
            var expectedException = new EnvironmentException("Error getting nodes");
            SetupErrorGettingNodes(expectedException);
            var actualException = Should.Throw<EnvironmentException>(() => _loadBalancer.TryGetNode(), "No nodes were discovered for service");
            actualException.InnerException.ShouldBe(expectedException);
        }

        private void SetupNoNodes()
        {
            SetupSourceNodes( /* no nodes */);
        }

        private void SetupSourceNodes(params Node[] nodes)
        {
            _getSourceNodes = () => nodes;
        }

        private void SetupErrorGettingNodes(Exception ex)
        {
            _getSourceNodes = () => throw ex;
        }

        private void SetupDefaultNodes()
        {
            SetupSourceNodes(_node1, _node2, _node3);
        }

        private async Task<Node[]> GetNodes(int times)
        {
            var tasks = Enumerable.Repeat(1, times).Select(_ => _loadBalancer.TryGetNode());
            var enumerable = tasks as Task<Node>[] ?? tasks.ToArray();
            await Task.WhenAll(enumerable);
            return enumerable.Select(t => t.Result).ToArray();
        }

        private Task<Node[]> Get20Nodes()
        {
            return GetNodes(20);
        }

        private HealthCheckResult GetHealthStatus()
        {
            var healthMonitor = (FakeHealthMonitor)_kernel.Get<IHealthMonitor>();
            return healthMonitor.Monitors[ServiceName].Invoke();
        }
    }
}
