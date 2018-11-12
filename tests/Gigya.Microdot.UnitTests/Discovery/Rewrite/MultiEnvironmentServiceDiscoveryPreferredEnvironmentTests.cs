using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
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

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class MultiEnvironmentServiceDiscoveryPreferredEnvironmentTests
    {
        private const string ServiceName = "ServiceName";
        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private DiscoveryConfig _discoveryConfig;
        public const int Repeat = 1;
        private const string ServiceVersion = "1.0.0.0";

        private IDiscovery _discovery;

        [SetUp]
        public async Task Setup()
        {
            _discovery = Substitute.For<IDiscovery>();

            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(_discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => _discoveryConfig);
            }, _configDic);

            _discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID)
                .ReturnsForAnyArgs(c =>
                {
                    return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                        ? new MasterLoadBalancer()
                        : c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "staging" ? (ILoadBalancer)new StagingLoadBalancer() : new PreferredEnvironmentLoadBalancer();
                });

            _discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
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

            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()("ServiceName", (x, y) => new Task(null));
            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor) _unitTestingKernel.Get<IHealthMonitor>()).Monitors["Discovery"]();
            Assert.IsTrue(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"{ServiceName}-canary"));

            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "preferred-host");
        }

        [Test]
        public async Task GotServiceFromMasterEnvironment()
        {
            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()("ServiceName", (x, y) => new Task(null));
            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors["Discovery"]();
            Assert.IsTrue(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"{ServiceName}-prod"));

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");
        }

        [Test]
        public async Task GotServiceFromStagingEnvironment()
        {
            IEnvironmentVariableProvider environmentMock = Substitute.For<IEnvironmentVariableProvider>();
            environmentMock.GetEnvironmentVariable("ENV").Returns("staging");
            environmentMock.GetEnvironmentVariable("ZONE").Returns("ZONE");
            environmentMock.GetEnvironmentVariable("REGION").Returns("REGION");
            environmentMock.GetEnvironmentVariable("CONSUL").Returns("CONSUL");
            _unitTestingKernel.Rebind<IEnvironmentVariableProvider>().ToConstant(environmentMock);

            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()("ServiceName", (x, y) => new Task(null));
            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors["Discovery"]();
            Assert.IsTrue(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"{ServiceName}-staging"));

            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "staging-host");

            environmentMock.ClearSubstitute();
        }

        [Test]
        public async Task FallbackToMasterEnvironment()
        {
            ILoadBalancer preferredEnvironmentLoadBalancerWithNoNodes = Substitute.For<ILoadBalancer>();
            preferredEnvironmentLoadBalancerWithNoNodes.TryGetNode().Returns(Task.FromResult<Node>(null));

            _discovery = Substitute.For<IDiscovery>();

            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().ToConstant(_discovery);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => _discoveryConfig);
            }, _configDic);

            _discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID)
                .ReturnsForAnyArgs(c =>
                {
                    return c.Arg<DeploymentIdentifier>().DeploymentEnvironment == "prod"
                        ? new MasterLoadBalancer()
                        : preferredEnvironmentLoadBalancerWithNoNodes;
                });

            _discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            IMultiEnvironmentServiceDiscovery serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()("ServiceName", (x, y) => new Task(null));

            _discoveryConfig.EnvironmentFallbackEnabled = false;//In case of preferred envitonmet, the fallback shoulf be performed without any conditions

            TracingContext.SetPreferredEnvironment("canary");

            var node = await serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors["Discovery"]();
            Assert.IsTrue(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"{ServiceName}-canary"));

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");
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
