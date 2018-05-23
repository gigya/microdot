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
        private Dictionary<DeploymentIdentifier, INodeSource> _consulNodeSources;        
        private HashSet<DeploymentIdentifier> _consulServiceList;        
        private IEnvironmentVariableProvider _environmentVariableProvider;        
        private IDateTime _dateTimeMock;
        private int id;
        private Func<DeploymentIdentifier, INodeSource> _getNodeSourceFromFactory;
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

            _configDic = new Dictionary<string, string> {{"Discovery.EnvironmentFallbackEnabled", "true"}};
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);
                k.Rebind<IDiscoveryFactory>().To<DiscoveryFactory>().InSingletonScope();

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
            _consulNodeSources = new Dictionary<DeploymentIdentifier, INodeSource>();            
            
            _consulServiceList = new HashSet<DeploymentIdentifier>();

            _getNodeSourceFromFactory = GetNodeSourceMock;
            var consulNodeSourceFactory = Substitute.For<INodeSourceFactory>();
            consulNodeSourceFactory.Type.Returns("Consul");
            consulNodeSourceFactory.TryCreateNodeSource(Arg.Any<DeploymentIdentifier>()).Returns(c=>_getNodeSourceFromFactory(c.Arg<DeploymentIdentifier>()));
            kernel.Rebind<INodeSourceFactory>().ToMethod(_ => consulNodeSourceFactory);            

            CreateConsulMock(MasterService);
            CreateConsulMock(OriginatingService);

        }

        private INodeSource CreateNodeSourceMock(DeploymentIdentifier di)
        {
            var mock = Substitute.For<INodeSource>();            
            mock.WasUndeployed.Returns(_ => !_consulServiceList.Contains(di));            
            return mock;
        }

        private INodeSource GetNodeSourceMock(DeploymentIdentifier di)
        {
            if (_consulNodeSources.ContainsKey(di))
                return _consulNodeSources[di];
            return CreateNodeSourceMock(di);
        }

        private void CreateConsulMock(DeploymentIdentifier di)
        {
            var mock = CreateNodeSourceMock(di);
            
            _consulNodeSources[di] = mock;

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
            var loadBalancer = await GetServiceDiscovery().GetLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulNodeSources[MasterService]);
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
        [Repeat(Repeat)]
        public async Task WhenServiceDeletedShouldFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var discovey = GetServiceDiscovery();
            
            var loadBalancer = await discovey.GetLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulNodeSources[OriginatingService]);            

            SetMockToReturnServiceNotDefined(OriginatingService);
            

            loadBalancer = await discovey.GetLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulNodeSources[MasterService]);            
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

            var loadBalancer = await discovey.GetLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulNodeSources[MasterService]);
            
            SetMockToReturnHost(OriginatingService);
            
            loadBalancer = await discovey.GetLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulNodeSources[OriginatingService]);            
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnError(OriginatingService);
            Should.Throw<EnvironmentException>(() => GetServiceDiscovery().GetLoadBalancer());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ServiceExistsShouldNotFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var loadBalancer = await GetServiceDiscovery().GetLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulNodeSources[OriginatingService]);            
        }

        [Test]
        [Repeat(Repeat)]
        public async Task MasterShouldNotFallBack()
        {
            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(MASTER_ENVIRONMENT);
            _unitTestingKernel.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

            SetMockToReturnServiceNotDefined(MasterService);

            Should.Throw<EnvironmentException>(()=> GetServiceDiscovery().GetLoadBalancer());
        }

        private void SetMockToReturnHost(DeploymentIdentifier di)
        {
            if (!_consulNodeSources.ContainsKey(di))
                CreateConsulMock(di);

            _consulServiceList.Add(di);            
        }

        private void SetMockToReturnServiceNotDefined(DeploymentIdentifier di)
        {
            _consulServiceList.Remove(di);            
        }

        private void SetMockToReturnError(DeploymentIdentifier badDeploymentIdentifier)
        {
            var getNodeSourceFromFactory = _getNodeSourceFromFactory;
            _getNodeSourceFromFactory = di =>
            {
                if (di.Equals(badDeploymentIdentifier))
                    throw new EnvironmentException("Mock: some error");
                else
                    return getNodeSourceFromFactory(di);
            };
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
                    (n,c)=>Task.FromResult(true));
            return discovery;
        }


        private DeploymentIdentifier MasterService => new DeploymentIdentifier(_serviceName, MASTER_ENVIRONMENT);
        private DeploymentIdentifier OriginatingService => new DeploymentIdentifier(_serviceName, ORIGINATING_ENVIRONMENT);        

    }
}