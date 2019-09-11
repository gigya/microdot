using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Events;
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
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class MultiEnvironmentServiceDiscoveryPreferredEnvironmentTests
    {
        private string ServiceName;
        private const string Canary = "canary";
        private const string Prod = "prod";
        private const string Staging = "staging";
        private const string ProdHost = "prod-host";
        private const string StagingHost = "staging-host";
        private const string CanaryHost = "canary-host";

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
            ServiceName = TestContext.CurrentContext.Test.FullName;
            IDiscovery discovery = Substitute.For<IDiscovery>();
            _discoveryConfig = new DiscoveryConfig
            {
                Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()),               
            };

            Dictionary<string, string> configDic = new Dictionary<string, string>();
            _currentEnvironment = Prod;
            var environment = Substitute.For<IEnvironment>();
            environment.DeploymentEnvironment.Returns(_ => _currentEnvironment);
            _unitTestingKernel = new TestingKernel<ConsoleLog>(mockConfig: configDic);
            _unitTestingKernel.Rebind<IEnvironment>().ToConstant(environment);
            _unitTestingKernel.Rebind<IDiscovery>().ToConstant(discovery);
            _unitTestingKernel.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => _discoveryConfig);

            _loadBalancerByEnvironment = new Dictionary<string, ILoadBalancer>();
            _loadBalancerByEnvironment.Add(Prod, new MasterLoadBalancer());
            _loadBalancerByEnvironment.Add(Staging, new StagingLoadBalancer());
            _loadBalancerByEnvironment.Add(Canary, new PreferredEnvironmentLoadBalancer());
            discovery.CreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>(), TrafficRoutingStrategy.RandomByRequestID)
                .ReturnsForAnyArgs(c => _loadBalancerByEnvironment[c.Arg<DeploymentIdentifier>().DeploymentEnvironment]);
            _serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>>()(ServiceName, (x, y) => new Task(null));

            TracingContext.SetPreferredEnvironment(null);
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
            _loadBalancerByEnvironment.Clear();
            _serviceDiscovery = null;
        }

        [Test]
        public async Task GotServiceFromPreferredEnvironment()
        {
            TracingContext.SetPreferredEnvironment(Canary);

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, CanaryHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
        }

        [Test]
        public async Task GotServiceFromStagingEnvironment()
        {
            _currentEnvironment = Staging;

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, StagingHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
        }

        [Test]
        public async Task FallbackFromPreferredToMasterEnvironment()
        {
            _loadBalancerByEnvironment[Canary] = ServiceUndeployedLoadBalancer();

            TracingContext.SetPreferredEnvironment(Canary);

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, ProdHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);

        }

        [Test]
        public async Task FallbackFromStagingToMasterEnvironment()
        {
            _currentEnvironment = Staging;
            _loadBalancerByEnvironment[Staging] = ServiceUndeployedLoadBalancer();
            _discoveryConfig.EnvironmentFallbackEnabled = true;
            _discoveryConfig.EnvironmentFallbackTarget = null; //when null, default of prod will be used

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<MasterLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, ProdHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"falling back to '{Prod}' environment"));
        }

        [Test]
        public async Task FallbackFromStagingToCanaryConfigurableEnvironment()
        {
            _currentEnvironment = Staging;
            _loadBalancerByEnvironment[Staging] = ServiceUndeployedLoadBalancer();            
            _discoveryConfig.EnvironmentFallbackEnabled = true;
            _discoveryConfig.EnvironmentFallbackTarget = Canary;

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, CanaryHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"falling back to '{Canary}' environment"));
        }

        [Test]
        public async Task FallbackFromPreferredToCurrentEnvironment()
        {
            _currentEnvironment = Staging;
            _loadBalancerByEnvironment[Canary] = ServiceUndeployedLoadBalancer();

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment(Canary);

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<StagingLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, StagingHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
        }

        [Test]
        public async Task ServiceDiscoveryFlowAllDeployedwithoutOverrides()
        {
            _currentEnvironment = Staging;

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment(Canary);

            var node = await _serviceDiscovery.GetNode();
            Assert.IsInstanceOf<PreferredEnvironmentLoadBalancer>(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, CanaryHost);

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
        }

        [Test]
        public async Task ServiceDiscoveryFlowAllDeployedwithOverrides()
        {
            _currentEnvironment = Staging;

            _discoveryConfig.EnvironmentFallbackEnabled = true;

            TracingContext.SetPreferredEnvironment(Canary);
            TracingContext.SetHostOverride(ServiceName, "override-host");

            var node = await _serviceDiscovery.GetNode();
            Assert.IsNull(node.LoadBalancer);
            Assert.AreEqual(node.Node.Hostname, "override-host");

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsTrue(hResult.IsHealthy);
        }

        [Test]
        public void ServiceNotDeployed()
        {
            _currentEnvironment = Prod;
            _loadBalancerByEnvironment[Prod] = ServiceUndeployedLoadBalancer();

            _serviceDiscovery.GetNode().ShouldThrow<ServiceUnreachableException>();

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsFalse(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains("Service not deployed"));
        }

        [Test]
        public void FallbackDisabled()
        {
            _currentEnvironment = Staging;

            _loadBalancerByEnvironment[Staging] = ServiceUndeployedLoadBalancer();

            _discoveryConfig.EnvironmentFallbackEnabled = false;

            _serviceDiscovery.GetNode().ShouldThrow<ServiceUnreachableException>();

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsFalse(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains("Service not deployed (and fallback disabled)"));
        }

        [Test]
        public void ServiceDiscoveryFlowNoServiceDeployed()
        {
            _currentEnvironment = Staging;

            _loadBalancerByEnvironment[Prod] = ServiceUndeployedLoadBalancer();
            _loadBalancerByEnvironment[Staging] = ServiceUndeployedLoadBalancer();
            _loadBalancerByEnvironment[Canary] = ServiceUndeployedLoadBalancer();

            _discoveryConfig.EnvironmentFallbackEnabled = true;
            //wait for config to update
          
            TracingContext.SetPreferredEnvironment(Canary);

            _serviceDiscovery.GetNode().ShouldThrow<ServiceUnreachableException>();

            HealthCheckResult hResult = GetHealthResult();
            Assert.IsFalse(hResult.IsHealthy);
            Assert.IsTrue(hResult.Message.Contains($"Service not deployed to '{Staging}' environment, fallback enabled but service not deployed to '{Prod}' environment either"));            
        }

        private static ILoadBalancer ServiceUndeployedLoadBalancer()
        {
            ILoadBalancer environmentLoadBalancerWithNoNodes = Substitute.For<ILoadBalancer>();
            environmentLoadBalancerWithNoNodes.TryGetNode().Returns(Task.FromResult<Node>(null));
            return environmentLoadBalancerWithNoNodes;
        }

        private HealthCheckResult GetHealthResult()
        {
            return ((FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>()).Monitors[ServiceName]();
        }


        private class MasterLoadBalancer : ILoadBalancer
        {
            public void Dispose()
            { }

            public async Task<Node> TryGetNode()
            {
                return await Task.FromResult(new Node(ProdHost));
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
                return await Task.FromResult(new Node(StagingHost));
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
                return await Task.FromResult(new Node(CanaryHost));
            }

            public void ReportUnreachable(Node node, Exception ex = null)
            {
            }
        }        
    }
}
