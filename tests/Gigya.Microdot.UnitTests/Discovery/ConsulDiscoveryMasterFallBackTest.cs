using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Utils;
using Metrics;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ConsulDiscoveryMasterFallBackTest
    {
        private const string ServiceVersion = "1.2.30.1234";
        private string _serviceName;
        private const string MASTER_ENVIRONMENT = "prod";
        private const string ORIGINATING_ENVIRONMENT = "fake_env";
        private readonly TimeSpan _timeOut = TimeSpan.FromSeconds(5);
        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private Dictionary<string, ConsulClientMock> _consulClient;
        private IEnvironment _environment;
        private ManualConfigurationEvents _configRefresh;
        private IDateTime _dateTimeMock;
        private int id;
        private const int Repeat = 1;

        [SetUp]
        public void SetUp()
        {
            
            _serviceName = $"ServiceName{++id}";

            _environment = Substitute.For<IEnvironment>();
            _environment.Zone.Returns("il3");
            _environment.DeploymentEnvironment.Returns(ORIGINATING_ENVIRONMENT);

            _configDic = new Dictionary<string, string> {{"Discovery.EnvironmentFallbackEnabled", "true"}};
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().ToConstant(_environment);

                k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                SetupConsulClientMocks();
                k.Rebind<Func<string, IConsulClient>>().ToMethod(_ => (s => _consulClient[s]));

                _dateTimeMock = Substitute.For<IDateTime>();
                _dateTimeMock.Delay(Arg.Any<TimeSpan>()).Returns(c => Task.Delay(TimeSpan.FromMilliseconds(100)));
                k.Rebind<IDateTime>().ToConstant(_dateTimeMock);
            }, _configDic);
            _configRefresh = _unitTestingKernel.Get<ManualConfigurationEvents>();

            var environment = _unitTestingKernel.Get<IEnvironment>();
            Assert.AreEqual(_environment, environment);
        }

        [TearDown]
        public void Teardown()
        {
            _unitTestingKernel?.Dispose();
            _configDic?.Clear();
            _configDic = null;
            _configRefresh = null;
            _consulClient?.Clear();
            _consulClient = null;
        }

        private void SetupConsulClientMocks()
        {
            _consulClient = new Dictionary<string, ConsulClientMock>();

            CreateConsulMock(MasterService);
            CreateConsulMock(OriginatingService);

        }

        private void CreateConsulMock(string serviceName)
        {
            var mock = new ConsulClientMock();
            mock.SetResult(new EndPointsResult
            {
                EndPoints = new EndPoint[] {new ConsulEndPoint {HostName = "dumy", Version = ServiceVersion}},
                IsQueryDefined = true
            });            

            _consulClient[serviceName] = mock;
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
            foreach (var consulClient in _consulClient)
            {
                consulClient.Value.Dispose();
            }
        }

        [Test]
        [Repeat(Repeat)]
        public async Task QueryNotDefinedShouldFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);
            var nextHost = GetServiceDiscovey().GetNextHost();
            nextHost.Result.HostName.ShouldBe(MasterService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task FallBackToMasterShouldNotHaveOriginatingServiceHealth()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);
            var nextHost = await GetServiceDiscovey().GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == MasterService).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task NoFallBackShouldNotHavMasterServiceHealth()
        {
            SetMockToReturnServiceNotDefined(MasterService);
            SetMockToReturnHost(OriginatingService);
            var nextHost = await GetServiceDiscovey().GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == OriginatingService).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == MasterService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task CreateServiceDiscoveyWithoutGetNextHostNoServiceHealthShouldAppear()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);
            var serviceDiscovey = GetServiceDiscovey();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == MasterService);
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ScopeZoneShouldUseServiceNameAsConsoleQuery()
        {
           _unitTestingKernel.Get<Func<DiscoveryConfig>>()().Services[_serviceName].Scope = ServiceScope.Zone;
            SetMockToReturnHost(_serviceName);
            var nextHost = GetServiceDiscovey().GetNextHost();
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

            var discovey = GetServiceDiscovey();
            var waitForEvents = discovey.EndPointsChanged.WhenEventReceived(_timeOut);

            var nextHost = discovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(OriginatingService);

            SetMockToReturnServiceNotDefined(OriginatingService);
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
            SetMockToReturnServiceNotDefined(OriginatingService);

            var discovey = GetServiceDiscovey();

            var nextHost = discovey.GetNextHost();
            (await nextHost).HostName.ShouldBe(MasterService);

            var waitForEvents = discovey.EndPointsChanged.WhenEventReceived(_timeOut);
            SetMockToReturnHost(OriginatingService);
            await waitForEvents;

            nextHost = GetServiceDiscovey().GetNextHost();
            nextHost.Result.HostName.ShouldBe(OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnError(OriginatingService);
            var exception = Should.Throw<EnvironmentException>(() => GetServiceDiscovey().GetNextHost());
            exception.UnencryptedTags["responseLog"].ShouldBe("Error response log");
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

            var nextHost = GetServiceDiscovey().GetNextHost();
            (await nextHost).HostName.ShouldBe(OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public void MasterShouldNotFallBack()
        {
            _environment = Substitute.For<IEnvironment>();
            _environment.Zone.Returns("il3");
            _environment.DeploymentEnvironment.Returns(MASTER_ENVIRONMENT);
            _unitTestingKernel.Rebind<IEnvironment>().ToConstant(_environment);

            SetMockToReturnServiceNotDefined(MasterService);

            Should.Throw<EnvironmentException>(() => GetServiceDiscovey().GetNextHost());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task EndPointsChangedShouldNotFireWhenNothingChange()
        {
            TimeSpan reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();
            int numOfEvent = 0;
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            //in the first time can fire one or two event
            var discovey = GetServiceDiscovey();
            discovey.GetNextHost();
            discovey.EndPointsChanged.LinkTo(new ActionBlock<string>(x => numOfEvent++));
            Thread.Sleep(200);
            numOfEvent = 0;

            for (int i = 0; i < 5; i++)
            {
                discovey.GetNextHost();
                Thread.Sleep((int) reloadInterval.TotalMilliseconds * 10);
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
            var discovey = GetServiceDiscovey();
            var waitForEvents = discovey.EndPointsChanged.StartCountingEvents();

            await discovey.GetNextHost();

            _configDic[$"Discovery.Services.{_serviceName}.Hosts"] = "localhost";
            _configDic[$"Discovery.Services.{_serviceName}.Source"] = "Config";

            Task waitForChangeEvent = waitForEvents.WhenNextEventReceived();
            await _configRefresh.ApplyChanges<DiscoveryConfig>();
            await waitForChangeEvent;
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
            var discovey = GetServiceDiscovey();

            //wait for discovey to be initialize!!
            var endPoints = await discovey.GetAllEndPoints();
            endPoints.Single().HostName.ShouldBe(OriginatingService);

            var waitForEvents = discovey.EndPointsChanged.StartCountingEvents();

            _configDic[$"Discovery.Services.{_serviceName}.Source"] = "Config";
            _configDic[$"Discovery.Services.{_serviceName}.Hosts"] = "localhost";
            Console.WriteLine("RaiseChangeEvent");

            Task waitForChangeEvent = waitForEvents.WhenNextEventReceived();
            _configRefresh.RaiseChangeEvent();
            await waitForChangeEvent;
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
            var discovey = GetServiceDiscovey();
            await discovey.GetAllEndPoints();

            var wait = discovey.EndPointsChanged.StartCountingEvents();
            bool UseOriginatingService(int i) => i % 2 == 0;
            for (int i = 1; i < 6; i++)
            {
                var waitForNextEvent = wait.WhenNextEventReceived();
                //act                
                if (UseOriginatingService(i))
                    SetMockToReturnHost(OriginatingService);
                else
                    SetMockToReturnServiceNotDefined(OriginatingService);

                await waitForNextEvent;
                //assert
                wait.ReceivedEvents.Count.ShouldBe(i);
                var nextHost = (await discovey.GetNextHost()).HostName;
                if (UseOriginatingService(i))
                    nextHost.ShouldBe(OriginatingService);
                else
                    nextHost.ShouldBe(MasterService);
            }
        }

        private void SetMockToReturnHost(string serviceName)
        {
            if (!_consulClient.ContainsKey(serviceName))
                CreateConsulMock(serviceName);

            _consulClient[serviceName].SetResult(
                new EndPointsResult
                {
                    EndPoints = new EndPoint[] {new ConsulEndPoint {HostName = serviceName, Version = ServiceVersion}},
                    RequestLog = "<Mock consul request log>",
                    ResponseLog = "<Mock consul response>",
                    ActiveVersion = ServiceVersion,
                    IsQueryDefined = true
                });            
        }

        private void SetMockToReturnServiceNotDefined(string serviceName)
        {
            _consulClient[serviceName].SetResult(new EndPointsResult {IsQueryDefined = false});            
        }

        private void SetMockToReturnError(string serviceName)
        {
            _consulClient[serviceName].SetResult(
                new EndPointsResult
                {
                    Error = new EnvironmentException("Mock: some error"),
                    IsQueryDefined = true,
                    ResponseLog = "Error response log"
                });
        }

        [Test]
        public void ServiceDiscoveySameNameShouldBeTheSame()
        {
            Assert.AreEqual(GetServiceDiscovey(), GetServiceDiscovey());
        }

        private readonly ReachabilityChecker _reachabilityChecker = x => Task.FromResult(true);

        private IServiceDiscovery GetServiceDiscovey()
        {
            var discovery =
                _unitTestingKernel.Get<Func<string, ReachabilityChecker, IServiceDiscovery>>()(_serviceName,
                    _reachabilityChecker);
            Task.Delay(200).GetAwaiter()
                .GetResult(); // let ConsulClient return the expected result before getting the dicovery object
            return discovery;
        }


        private string MasterService => ConsulServiceName(_serviceName, MASTER_ENVIRONMENT);
        private string OriginatingService => ConsulServiceName(_serviceName, ORIGINATING_ENVIRONMENT);

        private static string ConsulServiceName(string serviceName, string deploymentEnvironment) =>
            $"{serviceName}-{deploymentEnvironment}";
    }
}