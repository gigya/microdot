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
        private Dictionary<string, ILoadBalancer> _loadBalancerByEnvironment;
        private object _currentEnvironment;
        private DiscoveryConfig _discoveryConfig;
        private IMultiEnvironmentServiceDiscovery _serviceDiscovery;

        [SetUp]
        public async Task Setup()
        {
            IDiscovery discovery = Substitute.For<IDiscovery>();
            _discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };

            Dictionary<string, string> configDic = new Dictionary<string, string>();
            _currentEnvironment = "prod";
            var environment = Substitute.For<IEnvironment>();
            environment.DeploymentEnvironment.Returns(_ => _currentEnvironment);
            _unitTestingKernel = new TestingKernel<ConsoleLog>(mockConfig: configDic);
            _unitTestingKernel.Rebind<IEnvironment>().ToConstant(environment);
            _unitTestingKernel.Rebind<IDiscovery>().ToConstant(discovery);
            _unitTestingKernel.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => _discoveryConfig);

            _loadBalancerByEnvironment = new Dictionary<string, ILoadBalancer>();
            _loadBalancerByEnvironment.Add("prod", new MasterLoadBalancer());
            _loadBalancerByEnvironment.Add("staging", new StagingLoadBalancer());
            _loadBalancerByEnvironment.Add("canary", new PreferredEnvironmentLoadBalancer());
            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID)
                .ReturnsForAnyArgs(c => _loadBalancerByEnvironment[c.Arg<DeploymentIdentifier>().DeploymentEnvironment]);
            _serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));
            TracingContext.SetUpStorage();
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

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor) _unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "preferred-host");
        }

        [Test]
        public async Task FallbackFromCurrentToMasterEnvironment()
        {
            _currentEnvironment = "staging";
            _loadBalancerByEnvironment["staging"] = ServiceUndeployedLoadBalancer();
            _discoveryConfig.EnvironmentFallbackEnabled = true;

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");
        }

        [Test]
        public async Task GotServiceFromStagingEnvironment()
        {
            _currentEnvironment = "staging";            

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "staging-host");
        }

        [Test]
        public async Task FallbackFromPreferredToMasterEnvironment()
        {
            _loadBalancerByEnvironment["canary"] = ServiceUndeployedLoadBalancer();

            _discoveryConfig.EnvironmentFallbackEnabled = true; // Even for preferred environmet, fallback is performed only if fallback is enabled

            TracingContext.SetPreferredEnvironment("canary");

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");
        }

        [Test]
        public async Task ServiceDiscoveryFlowOnlyMasterIsDeployed()
        {
            _currentEnvironment = "staging";
            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");
            _loadBalancerByEnvironment["staging"] = ServiceUndeployedLoadBalancer();
            _loadBalancerByEnvironment["canary"] = ServiceUndeployedLoadBalancer();

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "prod-host");            
        }

        [Test]
        public async Task ServiceDiscoveryFlowOriginatingAndMasterDeployed()
        {
            _currentEnvironment = "staging";
            _loadBalancerByEnvironment["canary"] = ServiceUndeployedLoadBalancer();

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "staging-host");
        }

        [Test]
        public async Task ServiceDiscoveryFlowAllDeployedwithoutOverrides()
        {
            _currentEnvironment = "staging";

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            var node = await _serviceDiscovery.GetNode();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "preferred-host");
        }

        [Test]
        public async Task ServiceDiscoveryFlowAllDeployedwithOverrides()
        {
            _currentEnvironment = "staging";

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");
            TracingContext.SetHostOverride(ServiceName, "override-host");

            var node = await _serviceDiscovery.GetNode();


            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsTrue(hResult.IsHealthy);

            Assert.IsNull(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "override-host");
        }

        [Test]
        public void ServiceDiscoveryFlowNoServiceDeployed()
        {
            _currentEnvironment = "staging";

            _loadBalancerByEnvironment["prod"] = ServiceUndeployedLoadBalancer();
            _loadBalancerByEnvironment["staging"] = ServiceUndeployedLoadBalancer();
            _loadBalancerByEnvironment["canary"] = ServiceUndeployedLoadBalancer();

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment("canary");

            _serviceDiscovery.GetNode().ShouldThrow<ServiceUnreachableException>();

            HealthCheckResult hResult = ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
            Assert.IsFalse(hResult.IsHealthy);
        }

        private static ILoadBalancer ServiceUndeployedLoadBalancer()
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
