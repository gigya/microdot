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
using Gigya.Microdot.Testing.Utils;
using Metrics;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture]
    public class ConsulQueryDiscoveryMasterFallBackTest
    {
        private string _serviceName;
        private const string MASTER_ENVIRONMENT = "prod";
        private const string ORIGINATING_ENVIRONMENT = "MyFakeEnv";
        private readonly TimeSpan _timeOut = TimeSpan.FromSeconds(5);
        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private IConsulClient _consulClientMock;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ManualConfigurationEvents _configRefresh;
        private int id;
        private const int Repeat = 1;

        private Dictionary<string, EndPointsResult> _consulResult;

        [SetUp]
        public void SetUp()
        {
            _unitTestingKernel?.Dispose();
            _serviceName = $"ServiceName{++id}";

            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(ORIGINATING_ENVIRONMENT);

            _configDic = new Dictionary<string, string> { { "Discovery.EnvironmentFallbackEnabled", "true" } };
            _configDic["Discovery.Source"] = "ConsulQuery";
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

                k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                k.Rebind<IConsulClient>().ToConstant(CreateConsulClientMock());
            }, _configDic);
            _configRefresh = _unitTestingKernel.Get<ManualConfigurationEvents>();

            var environmentVariableProvider = _unitTestingKernel.Get<IEnvironmentVariableProvider>();
            Assert.AreEqual(_environmentVariableProvider, environmentVariableProvider);
        }

        private IConsulClient CreateConsulClientMock()
        {
            _consulResult = new Dictionary<string, EndPointsResult>();

            _consulResult[MasterService] = new EndPointsResult { EndPoints = new EndPoint[] { new ConsulEndPoint { HostName = "dumy"} }, IsQueryDefined = true};
            _consulResult[OriginatingService] = new EndPointsResult { EndPoints = new EndPoint[] { new ConsulEndPoint { HostName = "dumy" } }, IsQueryDefined = true };

            _consulClientMock = Substitute.For<IConsulClient>();
            _consulClientMock.GetQueryEndpoints(Arg.Any<string>())
                .Returns(c => Task.FromResult(_consulResult[c.Arg<string>()]));

            return _consulClientMock;
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
        }

        [Test]
        [Repeat(Repeat)]
        public void QueryNotDefinedShouldFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnQueryNotFound(OriginatingService);
            var nextHost = GetServiceDiscovey.GetNextHost();
            nextHost.Result.HostName.ShouldBe(MasterService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task FallBackToMasterShouldNotHaveOriginatingServiceHealth()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnQueryNotFound(OriginatingService);
            var nextHost = await GetServiceDiscovey.GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == MasterService).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task NoFallBackShouldNotHavMasterServiceHealth()
        {
            SetMockToReturnQueryNotFound(MasterService);
            SetMockToReturnHost(OriginatingService);
            var nextHost = await GetServiceDiscovey.GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == OriginatingService).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == MasterService);
        }

        [Test]
        [Repeat(Repeat)]
        public void CreateServiceDiscoveyWithoutGetNextHostNoServiceHealthShouldAppear()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnQueryNotFound(OriginatingService);
            var serviceDiscovey = GetServiceDiscovey;
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == MasterService);
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ScopeDataCenterShouldUseServiceNameAsConsoleQuery()
        {
            _configDic[$"Discovery.Services.{_serviceName}.Scope"] = "DataCenter";
            SetMockToReturnHost(_serviceName);
            var nextHost = GetServiceDiscovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(_serviceName);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenQueryDeleteShouldFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var discovey = GetServiceDiscovey;
            var waitForEvents = discovey.EndPointsChanged.WhenEventReceived(_timeOut);

            var nextHost = discovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(OriginatingService);

            SetMockToReturnQueryNotFound(OriginatingService);
            await waitForEvents;

            nextHost = discovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(MasterService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenQueryAddShouldNotFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnQueryNotFound(OriginatingService);

            var discovey = GetServiceDiscovey;
            var waitForEvents = discovey.EndPointsChanged.WhenEventReceived(_timeOut);

            var nextHost = discovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(MasterService);
            SetMockToReturnHost(OriginatingService);

            await waitForEvents;
            nextHost = GetServiceDiscovey.GetNextHost();
            nextHost.Result.HostName.ShouldBe(OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public void ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnError(OriginatingService);
            var exception = Should.Throw<EnvironmentException>(() => GetServiceDiscovey.GetNextHost());
            exception.UnencryptedTags["responseLog"].ShouldBe("Mock: some error");
            exception.UnencryptedTags["queryDefined"].ShouldBe("True");
            exception.UnencryptedTags["consulError"].ShouldNotBeNullOrEmpty();
            exception.UnencryptedTags["requestedService"].ShouldBe(OriginatingService);

        }

        [Test]
        [Repeat(Repeat)]
        public async Task QueryDefinedShouldNotFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var nextHost = GetServiceDiscovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public void MasterShouldNotFallBack()
        {
            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(MASTER_ENVIRONMENT);
            _unitTestingKernel.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

            SetMockToReturnQueryNotFound(MasterService);

            Should.Throw<EnvironmentException>(() => GetServiceDiscovey.GetNextHost());
        }

        [Test]
        [Repeat(Repeat)]
        public void EndPointsChangedShouldNotFireWhenNothingChange()
        {
            TimeSpan reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();
            int numOfEvent = 0;
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

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
        [Repeat(Repeat)]
        public async Task EndPointsChangedShouldFireConfigChange()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            //in the first time can fire one or two event
            var discovey = GetServiceDiscovey;
            var waitForEvents = discovey.EndPointsChanged.StartCountingEvents();

            await discovey.GetNextHost();

            _configDic[$"Discovery.Services.{_serviceName}.Hosts"] = "localhost";
            _configDic[$"Discovery.Services.{_serviceName}.Source"] = "Config";

            _configRefresh.RaiseChangeEvent();
            await waitForEvents.WhenNextEventReceived();
            var host = await discovey.GetNextHost();
            host.HostName.ShouldBe("localhost");
            waitForEvents.ReceivedEvents.Count.ShouldBe(1);

        }

        [Test]
        [Repeat(Repeat)]
        public async Task GetAllEndPointsChangedShouldFireConfigChange()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            //in the first time can fire one or two event
            var discovey = GetServiceDiscovey;

            //wait for discovey to be initialize!!
            var endPoints = await discovey.GetAllEndPoints();
            endPoints.Single().HostName.ShouldBe(OriginatingService);

            var waitForEvents = discovey.EndPointsChanged.StartCountingEvents();


            _configDic[$"Discovery.Services.{_serviceName}.Source"] = "Config";
            _configDic[$"Discovery.Services.{_serviceName}.Hosts"] = "localhost";
            Console.WriteLine("RaiseChangeEvent");
            _configRefresh.RaiseChangeEvent();

            await waitForEvents.WhenNextEventReceived();
            waitForEvents.ReceivedEvents.Count.ShouldBe(1);


            endPoints = await discovey.GetAllEndPoints();
            endPoints.Single().HostName.ShouldBe("localhost");
            waitForEvents.ReceivedEvents.Count.ShouldBe(1);

        }

        [Test]
        [Repeat(Repeat)]
        public async Task EndPointsChangedShouldFireWhenHostChange()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);
            var discovey = GetServiceDiscovey;
            await discovey.GetAllEndPoints();

            var wait = discovey.EndPointsChanged.StartCountingEvents();

            bool UseOriginatingService(int i) => i % 2 == 0;
            for (int i = 1; i < 6; i++)
            {
                //act
                if (UseOriginatingService(i))
                    SetMockToReturnHost(OriginatingService);
                else
                    SetMockToReturnQueryNotFound(OriginatingService);

                await wait.WhenNextEventReceived();
                //assert
                wait.ReceivedEvents.Count.ShouldBe(i);

                var nextHost = (await discovey.GetNextHost()).HostName;
                if (UseOriginatingService(i))
                    nextHost.ShouldBe(OriginatingService);
                else
                    nextHost.ShouldBe(MasterService);
            }
        }

        private void SetMockToReturnHost(string query)
        {
            _consulResult[query] = new EndPointsResult
                {
                    EndPoints = new EndPoint[] {new ConsulEndPoint { HostName = query }},
                    RequestLog = "<Mock consul request log>",
                    ResponseLog = "<Mock consul response>",
                    IsQueryDefined = true
                };
        }

        private void SetMockToReturnQueryNotFound(string query)
        {
            _consulResult[query] = ConsulClient.ErrorResult("<Mock consul request log>", null, false);
        }

        private void SetMockToReturnError(string query)
        {
            _consulResult[query] = ConsulClient.ErrorResult("<Mock consul request log>", new EnvironmentException("Mock: some error"), true);
        }

        [Test]
        public void ServiceDiscoveySameNameShouldBeTheSame()
        {
            Assert.AreEqual(GetServiceDiscovey, GetServiceDiscovey);
        }

        private readonly ReachabilityChecker _reachabilityChecker = x => Task.FromResult(true);
        private IServiceDiscovery GetServiceDiscovey => _unitTestingKernel.Get<Func<string, ReachabilityChecker, IServiceDiscovery>>()(_serviceName, _reachabilityChecker);

        private string MasterService => ConsulServiceName(_serviceName, MASTER_ENVIRONMENT);
        private string OriginatingService => ConsulServiceName(_serviceName, ORIGINATING_ENVIRONMENT);

        private static string ConsulServiceName(string serviceName, string deploymentEnvironment) => $"{serviceName}-{deploymentEnvironment}";
    }
}