using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.Testing;

using Metrics;

using Ninject;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture]
    public class ServiceDiscoveryMasterFallBackTest
    {
        private const string SERVICE_NAME = "ServiceName";
        private const string MASTER_ENVIRONMENT = "prod";
        private const string ORIGINATING_ENVIRONMENT = "MyFakeEnv";

        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private IConsulClient _consulAdapterMock;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ManualConfigurationEvents _configRefresh;

        [SetUp]
        public void Setup()
        {
            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(ORIGINATING_ENVIRONMENT);

            _configDic = new Dictionary<string, string> { { "Discovery.EnvironmentFallbackEnabled", "true" } };
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

                k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                _consulAdapterMock = Substitute.For<IConsulClient>();
                _consulAdapterMock.GetEndPoints(Arg.Any<string>()).Returns(Task.FromResult(new EndPointsResult { EndPoints = new[] { new ConsulEndPoint { HostName = "dumy" } } }));
                k.Rebind<IConsulClient>().ToConstant(_consulAdapterMock);
            }, _configDic);
            _configRefresh = _unitTestingKernel.Get<ManualConfigurationEvents>();
            var environmentVariableProvider = _unitTestingKernel.Get<IEnvironmentVariableProvider>();
            Assert.AreEqual(_environmentVariableProvider, environmentVariableProvider);
        }

        [Test]
        public void QueryNotDefinendShouldFallBackToMaster()
        {
            SetMockToReturnHost(_masterService);
            SetMockToReturnQueryNotFound(_originatingService);
            var nextHost = GetServiceDiscovey.GetNextHost();
            nextHost.Result.HostName.ShouldBe(_masterService);
        }
        [Test]
        public async Task FallBackToMasterShouldNotHaveOriginatingServiceHealth()
        {
            SetMockToReturnHost(_masterService);
            SetMockToReturnQueryNotFound(_originatingService);
            var nextHost = await GetServiceDiscovey.GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == _masterService).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == _originatingService);
            }

        [Test]
        public async Task NoFallBackShouldNotHavMasterServiceHealth()
        {
            SetMockToReturnQueryNotFound(_masterService);
            SetMockToReturnHost(_originatingService);
            var nextHost = await GetServiceDiscovey.GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == _originatingService).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == _masterService);
        }

        [Test]
        public void CreateSeviceDiscoveyWithoutGetNextHostNoServiceHealthShouldAppear()
        {
            SetMockToReturnHost(_masterService);
            SetMockToReturnQueryNotFound(_originatingService);
            var serviceDiscovey = GetServiceDiscovey;
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == _masterService);
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == _originatingService);
        }

        [Test]
        public async Task ScopeDataCenterShouldUseServiceNameAsConsoleQuery()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Scope"] = "DataCenter";
            SetMockToReturnHost(SERVICE_NAME);
            var nextHost = GetServiceDiscovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(SERVICE_NAME);
        }

        [Test]
        public async Task WhenQueryDeleteShouldFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{SERVICE_NAME}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(_masterService);
            SetMockToReturnHost(_originatingService);

            var nextHost = GetServiceDiscovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(_originatingService);

            SetMockToReturnQueryNotFound(_originatingService);
            Thread.Sleep((int)reloadInterval.TotalMilliseconds * 5);
            nextHost = GetServiceDiscovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(_masterService);
        }

        [Test]
        public  async Task WhenQueryAddShouldNotFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{SERVICE_NAME}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(_masterService);
            SetMockToReturnQueryNotFound(_originatingService);
            var nextHost = GetServiceDiscovey.GetNextHost();

            (await nextHost).HostName.ShouldBe(_masterService);
            SetMockToReturnHost(_originatingService);

            await Task.Delay((int)reloadInterval.TotalMilliseconds * 10);
            nextHost = GetServiceDiscovey.GetNextHost();
            nextHost.Result.HostName.ShouldBe(_originatingService);
        }

        [Test]
        public void ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(_masterService);
            SetMockToReturnError(_originatingService);
            Should.Throw<EnvironmentException>(() => GetServiceDiscovey.GetNextHost());
        }

        [Test]
        public async Task QueryDefinendShouldNotFallBackToMaster()
        {
            SetMockToReturnHost(_masterService);
            SetMockToReturnHost(_originatingService);

            var nextHost = GetServiceDiscovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(_originatingService);
        }

        [Test]
        public void MasterShouldNotFallBack()
        {
            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(MASTER_ENVIRONMENT);
            _unitTestingKernel.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

            SetMockToReturnQueryNotFound(_masterService);

            Should.Throw<EnvironmentException>(() => GetServiceDiscovey.GetNextHost());
        }

        [Test]
        public void EndPointsChangedShouldNotFireWhenNothingChange()
        {
            TimeSpan reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{SERVICE_NAME}.ReloadInterval"] = reloadInterval.ToString();
            int numOfEvent = 0;
            SetMockToReturnHost(_masterService);
            SetMockToReturnHost(_originatingService);

            //in the first time can fire one or two event
            var discovey = GetServiceDiscovey;
            discovey.GetNextHost();
            discovey.EndPointsChanged.LinkTo(new ActionBlock<string>(x => numOfEvent++));
            Thread.Sleep(200);
            numOfEvent = 0;

            for (int i = 0; i < 5; i++)
            {
                discovey.GetNextHost();
                Thread.Sleep((int)reloadInterval.TotalMilliseconds * 10);
            }
            numOfEvent.ShouldBe(0);
        }


        [Test]
        public async Task EndPointsChangedShouldFireConfigChange()
        {
            int numOfEvent = 0;
            SetMockToReturnHost(_masterService);
            SetMockToReturnHost(_originatingService);

            //in the first time can fire one or two event
            var discovey = GetServiceDiscovey;
            await discovey.GetNextHost();
            discovey.EndPointsChanged.LinkTo(new ActionBlock<string>(x => numOfEvent++));
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Source"] = "Config";
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Hosts"] = "localhost";

            _configRefresh.RaiseChangeEvent();
            Thread.Sleep(400);

            var host = await discovey.GetNextHost();
            host.HostName.ShouldBe("localhost");

            numOfEvent.ShouldBe(2);
        }


        [Test]
        public async Task EndPointsChangedShouldFireWhenHostChange()
        {
            TimeSpan reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{SERVICE_NAME}.ReloadInterval"] = reloadInterval.ToString();
            int numOfEvent = 0;
            SetMockToReturnHost(_masterService);
            SetMockToReturnQueryNotFound(_originatingService);

            var discovey = GetServiceDiscovey;
            discovey.EndPointsChanged.LinkTo(new ActionBlock<string>(x => numOfEvent++));

            //in the first time can fire one or two event
            await discovey.GetNextHost();
            int change = 0;

            for(int i = 0;i < 3;i++)
            {
                change++;
                SetMockToReturnQueryNotFound(_originatingService);
                Thread.Sleep((int)reloadInterval.TotalMilliseconds * 40);

                var nextHost = await discovey.GetNextHost();

                nextHost.HostName.ShouldBe(_masterService);
                SetMockToReturnHost(_originatingService);
                change++;

                Thread.Sleep((int)reloadInterval.TotalMilliseconds * 40);
                nextHost = await discovey.GetNextHost();
                nextHost.HostName.ShouldBe(_originatingService);
            }
            Thread.Sleep(300);
            numOfEvent.ShouldBe(change);
        }


        private void SetMockToReturnHost(string query)
        {
            _consulAdapterMock.GetEndPoints(query).Returns(Task.FromResult(ConsulClient.SuccessResult(new[] { new ConsulEndPoint { HostName = query } }, "<Mock consul request log>", "<Mock consul response>")));
        }

        private void SetMockToReturnQueryNotFound(string query)
        {
            _consulAdapterMock.GetEndPoints(query).Returns(Task.FromResult(ConsulClient.ErrorResult("<Mock consul request log>", new EnvironmentException("Mock: Query not found"), false)));
        }

        private void SetMockToReturnError(string query)
        {
            _consulAdapterMock.GetEndPoints(query).Returns(Task.FromResult(ConsulClient.ErrorResult("<Mock consul request log>", new EnvironmentException("Mock: some error"), true)));
        }

        [Test]
        public void ServiceDiscoveySameNameShouldBeTheSame()
        {
            Assert.AreEqual(GetServiceDiscovey, GetServiceDiscovey);
        }

        private readonly ReachabilityChecker _reachabilityChecker = x => Task.FromResult(true);
        private IServiceDiscovery GetServiceDiscovey => _unitTestingKernel.Get<Func<string, ReachabilityChecker, IServiceDiscovery>>()(SERVICE_NAME, _reachabilityChecker);

        private string _masterService = ConsulServiceName(SERVICE_NAME, MASTER_ENVIRONMENT);
        private string _originatingService = ConsulServiceName(SERVICE_NAME, ORIGINATING_ENVIRONMENT);

        private static string ConsulServiceName(string serviceName, string deploymentEnvironment) => $"{serviceName}-{deploymentEnvironment}";
    }
}