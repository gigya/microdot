using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Gigya.Microdot.Testing.Shared;
using Metrics;
using Ninject;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class MultiEnvironmentServiceDiscoveryPreferredEnvironmentTests
    {
        private const string ServiceName = "ServiceName";
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        public const int Repeat = 1;
        private const string ServiceVersion = "1.0.0.0";

        [SetUp]
        public async Task Setup()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;

            Dictionary<string, string> configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID)
                .ReturnsForAnyArgs(c =>
                {
                    return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                        ? new MasterLoadBalancer()
                        : c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "staging" ? (ILoadBalancer)new StagingLoadBalancer() : new PreferredEnvironmentLoadBalancer();
                });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
        }

        [Test]
        public async Task GotServiceFromPreferredEnvironment()
        {
            TracingContext.SetPreferredEnvironment("canary");

            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));
            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor) _unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "preferred-host");
        }

        [Test]
        public async Task GotServiceFromMasterEnvironment()
        {
            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));
            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");
        }

        [Test]
        public async Task GotServiceFromStagingEnvironment()
        {
            IEnvironmentVariableProvider environmentMock = GetStagingEnvironmentMock(_unitTestingKernel);

            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));
            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "staging-host");

            environmentMock.ClearSubstitute();
        }

        [Test]
        public async Task FallbackToMasterEnvironment()
        {
            ILoadBalancer preferredEnvironmentLoadBalancerWithNoNodes = GetLoadBalancerMockWithoutNodes();

            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;
            Dictionary<string, string> configDic = new Dictionary<string, string>();

            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID)
                .ReturnsForAnyArgs(c =>
                {
                    return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                        ? new MasterLoadBalancer()
                        : preferredEnvironmentLoadBalancerWithNoNodes;
                });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            discoveryConfig.EnvironmentFallbackEnabled = false;//In case of preferred envitonmet, the fallback shoulf be performed without any conditions

            TracingContext.SetPreferredEnvironment("canary");

            NodeAndLoadBalancer node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");
        }

        [Test]
        public async Task ServiceDiscoveryFlowOnlyMasterHasNode()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;
            Dictionary<string, string> configDic = new Dictionary<string, string>();

            TestingKernel<ConsoleLog> unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            IEnvironmentVariableProvider environmentMock = GetStagingEnvironmentMock(unitTestingKernel);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID).ReturnsForAnyArgs(c =>
            {
                return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                    ? new MasterLoadBalancer()
                    : GetLoadBalancerMockWithoutNodes();
            });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            NodeAndLoadBalancer node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");

            environmentMock.ClearSubstitute();
        }

        [Test]
        public async Task ServiceDiscoveryFlowOriginatingAndMasterHaveNode()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;
            Dictionary<string, string> configDic = new Dictionary<string, string>();

            TestingKernel<ConsoleLog> unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            IEnvironmentVariableProvider environmentMock = GetStagingEnvironmentMock(unitTestingKernel);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID).ReturnsForAnyArgs(c =>
            {
                return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                    ? new MasterLoadBalancer()
                    : c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "staging" ? new StagingLoadBalancer() : GetLoadBalancerMockWithoutNodes();
            });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            NodeAndLoadBalancer node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "staging-host");

            environmentMock.ClearSubstitute();
        }

        [Test]
        public async Task ServiceDiscoveryFlowAllHaveNodewithoutOverrides()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;
            Dictionary<string, string> configDic = new Dictionary<string, string>();

            TestingKernel<ConsoleLog> unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            IEnvironmentVariableProvider environmentMock = GetStagingEnvironmentMock(unitTestingKernel);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID).ReturnsForAnyArgs(c =>
            {
                return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                    ? new MasterLoadBalancer()
                    : c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "staging" ? (ILoadBalancer)new StagingLoadBalancer() : new PreferredEnvironmentLoadBalancer();
            });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            NodeAndLoadBalancer node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "preferred-host");

            environmentMock.ClearSubstitute();
        }

        [Test]
        public async Task ServiceDiscoveryFlowAllHaveNodewithOverrides()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;
            Dictionary<string, string> configDic = new Dictionary<string, string>();

            TestingKernel<ConsoleLog> unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            IEnvironmentVariableProvider environmentMock = GetStagingEnvironmentMock(unitTestingKernel);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID).ReturnsForAnyArgs(c =>
            {
                return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                    ? new MasterLoadBalancer()
                    : c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "staging" ? (ILoadBalancer)new StagingLoadBalancer() : new PreferredEnvironmentLoadBalancer();
            });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");
            TracingContext.SetHostOverride(ServiceName, "override-host");

            NodeAndLoadBalancer node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsNull(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "override-host");

            environmentMock.ClearSubstitute();
        }

        [Test]
        public async Task ServiceDiscoveryFlowNoNodes()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            DiscoveryConfig discoveryConfig = null;
            Dictionary<string, string> configDic = new Dictionary<string, string>();

            TestingKernel<ConsoleLog> unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => discoveryConfig);
            }, configDic);

            IEnvironmentVariableProvider environmentMock = GetStagingEnvironmentMock(unitTestingKernel);

            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID).ReturnsForAnyArgs(c =>
                {
                    return GetLoadBalancerMockWithoutNodes();
                });

            discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            serviceDiscovery.GetNode().ShouldThrow<ServiceUnreachableException>();

            HealthCheckResult hResult = ((FakeHealthMonitor)unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsFalse(hResult.IsHealthy);
        }

        private IEnvironmentVariableProvider GetStagingEnvironmentMock(TestingKernel<ConsoleLog> kernel)
        {
            IEnvironmentVariableProvider environmentMock = Substitute.For<IEnvironmentVariableProvider>();
            environmentMock.GetEnvironmentVariable("ENV").Returns("staging");
            environmentMock.GetEnvironmentVariable("ZONE").Returns("ZONE");
            environmentMock.GetEnvironmentVariable("REGION").Returns("REGION");
            environmentMock.GetEnvironmentVariable("CONSUL").Returns("CONSUL");

            kernel.Rebind<IEnvironmentVariableProvider>().ToConstant(environmentMock);
            return environmentMock;
        }

        private static ILoadBalancer GetLoadBalancerMockWithoutNodes()
        {
            ILoadBalancer environmentLoadBalancerWithNoNodes = Substitute.For<ILoadBalancer>();
            environmentLoadBalancerWithNoNodes.TryGetNode().Returns(Task.FromResult<Node>(null));
            return environmentLoadBalancerWithNoNodes;
        }

        private class MasterLoadBalancer : ILoadBalancer
        {
            public void Dispose()
            {}

            public async Task<Node> TryGetNode()
            {
                return await Task.FromResult(new Node("prod-host"));
            }

            public void ReportUnreachable(Node node, Exception ex = null)
            {
            }
        }

        private class StagingLoadBalancer : ILoadBalancer
        {
            public void Dispose()
            { }

            public async Task<Node> TryGetNode()
            {
                return await Task.FromResult(new Node("staging-host"));
            }

            public void ReportUnreachable(Node node, Exception ex = null)
            {
            }
        }

        private class PreferredEnvironmentLoadBalancer : ILoadBalancer
        {
            public void Dispose()
            { }

            public async Task<Node> TryGetNode()
            {
                return await Task.FromResult(new Node("preferred-host"));
            }

            public void ReportUnreachable(Node node, Exception ex = null)
            {
            }
        }
    }
}
