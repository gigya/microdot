using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class NewServiceDiscoveryPreferredEnvironmentTests
    {
        private const string ServiceName = "ServiceName";
        private INewServiceDiscovery _serviceDiscovery;
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
                        ? (ILoadBalancer)new MasterLoadBalancer()
                        : new PreferredEnvironmentLoadBalancer();
                });

            _discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            _serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, INewServiceDiscovery>>()("ServiceName", (x, y) => new Task(null));
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

            KeyValuePair<Node, ILoadBalancer> node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.Value);
            Assert.AreEqual(node.Key.Hostname, "preferred-host");
        }

        [Test]
        public async Task GotServiceFromMasterEnvironment()
        {
            KeyValuePair<Node, ILoadBalancer> node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<MasterLoadBalancer>(node.Value);
            Assert.AreEqual(node.Key.Hostname, "prod-host");
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
            _serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, INewServiceDiscovery>>()("ServiceName", (x, y) => new Task(null));

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            KeyValuePair<Node, ILoadBalancer> node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<MasterLoadBalancer>(node.Value);
            Assert.AreEqual(node.Key.Hostname, "prod-host");
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
