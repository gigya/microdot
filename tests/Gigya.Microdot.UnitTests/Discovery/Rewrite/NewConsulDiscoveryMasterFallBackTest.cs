using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class NewConsulDiscoveryMasterFallBackTest
    {
        private const string ServiceVersion = "1.2.30.1234";
        private string _serviceName;
        private const string MASTER_ENVIRONMENT = "prod";
        private const string ORIGINATING_ENVIRONMENT = "fake_env";
        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private Dictionary<DeploymentIdentifier, ILoadBalancer> _loadBalancers;
        private Dictionary<DeploymentIdentifier, Func<Node>> _nodeResults;
        private HashSet<DeploymentIdentifier> _consulServiceList;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private IDateTime _dateTimeMock;
        private int id;
        private const int Repeat = 1;

        [SetUp]
        public void SetUp()
        {
            _unitTestingKernel?.Dispose();
            _serviceName = $"ServiceName{++id}";

            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(ORIGINATING_ENVIRONMENT);
            _environmentVariableProvider.ConsulAddress.Returns((string)null);

            _configDic = new Dictionary<string, string> { { "Discovery.EnvironmentFallbackEnabled", "true" } };
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);
                k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();

                SetupConsulMocks(k);

                _dateTimeMock = Substitute.For<IDateTime>();
                _dateTimeMock.Delay(Arg.Any<TimeSpan>()).Returns(c => Task.Delay(TimeSpan.FromMilliseconds(100)));
                k.Rebind<IDateTime>().ToConstant(_dateTimeMock);
            }, _configDic);

            var environmentVariableProvider = _unitTestingKernel.Get<IEnvironmentVariableProvider>();
            Assert.AreEqual(_environmentVariableProvider, environmentVariableProvider);
        }

        private void SetupConsulMocks(IKernel kernel)
        {
            _loadBalancers = new Dictionary<DeploymentIdentifier, ILoadBalancer>();
            _nodeResults = new Dictionary<DeploymentIdentifier, Func<Node>>();

            _consulServiceList = new HashSet<DeploymentIdentifier>();

            var discovery = Substitute.For<IDiscovery>();            
            discovery.TryCreateLoadBalancer(Arg.Any<DeploymentIdentifier>(), Arg.Any<ReachabilityCheck>()).Returns(c => GetLoadBalancerMock(c.Arg<DeploymentIdentifier>()));
            kernel.Rebind<IDiscovery>().ToMethod(_ => discovery);

            CreateConsulMock(MasterService);
            CreateConsulMock(OriginatingService);

        }

        private ILoadBalancer CreateLoadBalancerMock(DeploymentIdentifier di)
        {
            var mock = Substitute.For<ILoadBalancer>();
            mock.WasUndeployed().Returns(_ => Task.FromResult(!_consulServiceList.Contains(di)));
            mock.GetNode().Returns(_ => _nodeResults[di]());
            return mock;
        }

        private ILoadBalancer GetLoadBalancerMock(DeploymentIdentifier di)
        {
            if (_loadBalancers.ContainsKey(di))
                return _loadBalancers[di];
            return CreateLoadBalancerMock(di);
        }

        private void CreateConsulMock(DeploymentIdentifier di)
        {
            var mock = CreateLoadBalancerMock(di);

            _nodeResults[di] = () => new ConsulNode(hostName: "dummy", version: ServiceVersion) ;
            _loadBalancers[di] = mock;

            _consulServiceList.Add(di);
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ServiceNotExistsShouldFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);
            var nextHost = (await GetServiceDiscovery().GetLoadBalancer()).GetNode();
            (await nextHost).Hostname.ShouldBe(HostnameFor(MasterService));
        }

        [Test]
        [Repeat(Repeat)]
        public async Task FallbackDisabledByConsul_ShouldNotFallBackToMaster()
        {
            _configDic[$"Discovery.EnvironmentFallbackEnabled"] = "false";

            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);

            Should.Throw<EnvironmentException>(() => GetServiceDiscovery().GetLoadBalancer());
        }

        [Test]
        [Ignore("New ServiceDiscovery implementation does not support DataCenter scopes")]
        [Repeat(Repeat)]
        public async Task ScopeDataCenterShouldUseServiceNameWithNoEnvironment()
        {
            _configDic[$"Discovery.Services.{_serviceName}.Scope"] = "DataCenter";
            SetMockToReturnHost(new DeploymentIdentifier(_serviceName));
            var nextHost = (await GetServiceDiscovery().GetLoadBalancer()).GetNode();
            (await nextHost).Hostname.ShouldBe(_serviceName);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenServiceDeletedShouldFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var discovey = GetServiceDiscovery();

            var node = (await discovey.GetLoadBalancer()).GetNode();
            (await node).Hostname.ShouldBe(HostnameFor(OriginatingService));

            SetMockToReturnServiceNotDefined(OriginatingService);


            node = (await discovey.GetLoadBalancer()).GetNode();
            (await node).Hostname.ShouldBe(HostnameFor(MasterService));
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenServiceAddedShouldNotFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);

            var discovey = GetServiceDiscovery();

            var node = (await discovey.GetLoadBalancer()).GetNode();
            (await node).Hostname.ShouldBe(HostnameFor(MasterService));

            SetMockToReturnHost(OriginatingService);

            node = (await discovey.GetLoadBalancer()).GetNode();
            node.Result.Hostname.ShouldBe(HostnameFor(OriginatingService));
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnError(OriginatingService);
            Should.Throw<EnvironmentException>(async () => (await GetServiceDiscovery().GetLoadBalancer()).GetNode());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ServiceExistsShouldNotFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var nextHost = (await GetServiceDiscovery().GetLoadBalancer()).GetNode();
            (await nextHost).Hostname.ShouldBe(HostnameFor(OriginatingService));
        }

        [Test]
        [Repeat(Repeat)]
        public void MasterShouldNotFallBack()
        {
            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(MASTER_ENVIRONMENT);
            _unitTestingKernel.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

            SetMockToReturnServiceNotDefined(MasterService);

            Should.Throw<EnvironmentException>(() => GetServiceDiscovery().GetLoadBalancer());
        }

        private void SetMockToReturnHost(DeploymentIdentifier di)
        {
            if (!_loadBalancers.ContainsKey(di))
                CreateConsulMock(di);

            var newNode = new Node(HostnameFor(di));
            _nodeResults[di] = () => newNode;

            _consulServiceList.Add(di);
        }

        private void SetMockToReturnServiceNotDefined(DeploymentIdentifier di)
        {
            _consulServiceList.Remove(di);
        }

        private void SetMockToReturnError(DeploymentIdentifier di)
        {
            _nodeResults[di] = () => throw new EnvironmentException("Mock: some error");
        }

        [Test]
        public void ServiceDiscoveySameNameShouldBeTheSame()
        {
            Assert.AreEqual(GetServiceDiscovery(), GetServiceDiscovery());
        }

        private INewServiceDiscovery GetServiceDiscovery()
        {
            var discovery =
                _unitTestingKernel.Get<Func<string, ReachabilityCheck, INewServiceDiscovery>>()(_serviceName,
                    (n, c) => Task.FromResult(true));
            return discovery;
        }


        private DeploymentIdentifier MasterService => new DeploymentIdentifier(_serviceName, MASTER_ENVIRONMENT);
        private DeploymentIdentifier OriginatingService => new DeploymentIdentifier(_serviceName, ORIGINATING_ENVIRONMENT);
        private string HostnameFor(DeploymentIdentifier di) => $"{di.DeploymentEnvironment}-host";

    }
}