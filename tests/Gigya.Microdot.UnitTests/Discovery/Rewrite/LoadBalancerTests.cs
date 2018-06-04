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

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
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

        private Node Node1 = new Node("Host1", 111);
        private Node Node2 = new Node("Host2", 222);
        private Node Node3 = new Node("Host3", 333);
        private Node Node4 = new Node("Host4", 444);
        private Node Node5 = new Node("Host5", 555);
        private Node Node6 = new Node("Host6", 666);

        private Func<Node[]> _getSourceNodes = () => new Node[0];

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _kernel = new TestingKernel<LogSpy>(k=>k.Rebind<IDiscovery>().ToMethod(_=>_discovery));
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
            _reachabilityCheck = (n,c) => throw new EnvironmentException("node is unreachable");                       
        }

        private void CreateLoadBalancer(TrafficRouting trafficRouting=TrafficRouting.RandomByRequestID)
        {
            var createLoadBalancer = _kernel.Get<Func<DeploymentIdentifier, ReachabilityCheck, TrafficRouting, ILoadBalancer>>();
            _loadBalancer = createLoadBalancer(
                new DeploymentIdentifier(ServiceName, Env),
                (n, c) => _reachabilityCheck(n, c),
                trafficRouting);
        }

        [TearDown]
        public void TearDown()
        {
            _loadBalancer?.Dispose();
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_RoutingTrafficRoundRobin_GetDiffenent3NodesAfterExactly3Times()
        {
            CreateLoadBalancer(TrafficRouting.RoundRobin);
            SetupDefaultNodes();
            var allEndpoints = await GetNodes(times:3);
            allEndpoints.ShouldContain(Node1);
            allEndpoints.ShouldContain(Node2);
            allEndpoints.ShouldContain(Node3);
        }

        [Test]
        public async Task GetNode_ThreeNodes_ReturnsAllThree()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();

            var allEndpoints = await Get20Nodes();

            new[] { Node1, Node2, Node3 }.ShouldBeSubsetOf(allEndpoints);
        }

        [Test]
        public void GetNode_ThreeNodes_ShouldBeHealthy()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();

            _loadBalancer.GetNode();
            var healthStatus = GetHealthStatus();

            healthStatus.IsHealthy.ShouldBeTrue();            
        }

        [Test]
        public async Task GetNode_NodesChanged_ReturnsNewNodes()
        {
            CreateLoadBalancer();
            SetupSourceNodes(Node1,Node2,Node3);
            Get20Nodes();
            SetupSourceNodes(Node4, Node5, Node6);
            

            var res = await Get20Nodes();
            res.Distinct()
                .ShouldBe(new[] { Node4, Node5, Node6 }, true);
        }

        [Test]
        public void GetNode_NoNodes_Throws()
        {
            CreateLoadBalancer();
            SetupNoNodes();
            Should.Throw<EnvironmentException>(() =>_loadBalancer.GetNode());
        }

        [Test]
        [Repeat(Repeat)]
        public void GetNode_NodesListBecomesEmpty_Throws()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();
            Get20Nodes();
            SetupNoNodes();
            Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode());
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse();            
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_AfterNodeReportedUnreachable_NodeWillNotBeReturned()
        {
            CreateLoadBalancer();
            var allNodes = new[] {Node1, Node2, Node3};
            SetupSourceNodes(allNodes);

            var unreachableNode = await _loadBalancer.GetNode();
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
        public async Task GetNode_NodeIsReachableAgain_NodeWillBeReturned()
        {            
            CreateLoadBalancer();
            SetupDefaultNodes();

            var selectedNode = await _loadBalancer.GetNode();
            _loadBalancer.ReportUnreachable(selectedNode);            

            (await Get20Nodes()).ShouldNotContain(selectedNode);

            _reachabilityCheck = (_,__) => Task.FromResult(true);
            await Task.Delay(1000);

            (await Get20Nodes()).ShouldContain(selectedNode);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_OnlyOneNodeUnreachable_ShouldStillBeHealthy()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();        

            await Run20times(node =>
            {
                if (node.Equals(Node2))
                    _loadBalancer.ReportUnreachable(node);                    
            });

            var healthResult = GetHealthStatus();
            healthResult.IsHealthy.ShouldBeTrue();
            healthResult.Message.ShouldContain(Node2.ToString());
        }


        private async Task Run20times(Action<Node> act)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var node = await _loadBalancer.GetNode();
                    act(node);
                }
                catch
                {
                }
            }
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_NodeUnreachableThenReturnsInBackground_NodeShouldBeReturned()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();
            _reachabilityCheck = (_,__) => throw new EnvironmentException("node is unreachable");

            var selectedNode = await _loadBalancer.GetNode();
            _loadBalancer.ReportUnreachable(selectedNode);            

            (await Get20Nodes()).ShouldNotContain(selectedNode);

            var waitForReachablitiy = new TaskCompletionSource<bool>();
            _reachabilityCheck = (_,__) =>
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
            SetupSourceNodes(Node1,Node2,Node3);

            await Run20times(node =>_loadBalancer.ReportUnreachable(node));

            Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode());
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse(healthStatus.Message);
            healthStatus.Message.ShouldContain("All 3 Nodes are unreachable");
            healthStatus.Message.ShouldContain(Node1.ToString());
            healthStatus.Message.ShouldContain(Node2.ToString());
            healthStatus.Message.ShouldContain(Node3.ToString());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_AllNodesUnreachableThenAllNodesReachable_ReturnsAllNodes()
        {
            CreateLoadBalancer();
            SetupSourceNodes(Node1,Node2,Node3);
            await Run20times(node => _loadBalancer.ReportUnreachable(node));
            
            Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode());

            _reachabilityCheck = (_,__) => Task.FromResult(true);

            await Task.Delay(1000);

            var nodes = await Get20Nodes();
            nodes.ShouldContain(Node1);
            nodes.ShouldContain(Node2);
            nodes.ShouldContain(Node3);
        }


        [Test]
        [Repeat(Repeat)]
        public async Task GetNode_NodesUnreachableButReachabilityCheckThrows_ErrorIsLogged()
        {
            CreateLoadBalancer();
            SetupDefaultNodes();
            var reachabilityException = new Exception("Simulated error while running reachability check");

            _reachabilityCheck = (_,__) => { throw reachabilityException; };
            await Run20times(node => _loadBalancer.ReportUnreachable(node));

            await Task.Delay(1500);

            _log.LogEntries.ToArray().ShouldContain(e => e.Exception == reachabilityException);
        }

        [Test]        
        public async Task ErrorGettingNodes_MatchingExceptionIsThrown()
        {
            CreateLoadBalancer();
            var expectedException = new EnvironmentException("Error getting nodes");
            SetupErrorGettingNodes(expectedException);
            var actualException = Should.Throw<EnvironmentException>(() => _loadBalancer.GetNode(), "No nodes were discovered for service");
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
            SetupSourceNodes(Node1, Node2, Node3);
        }

        async Task<Node[]> GetNodes(int times)
        {
            var tasks = Enumerable.Repeat(1, times).Select(_ => _loadBalancer.GetNode());
            await Task.WhenAll(tasks);
            return tasks.Select(t => t.Result).ToArray();
        }

        Task<Node[]> Get20Nodes()
        {
            return GetNodes(20);
        }

        private HealthCheckResult GetHealthStatus()
        {
            var healthMonitor = (FakeHealthMonitor)_kernel.Get<IHealthMonitor>();
            return healthMonitor.Monitors[new DeploymentIdentifier(ServiceName,Env).ToString()].Invoke();
        }
    }

}
