using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    [TestFixture,NonParallelizable]
    public class ConsulDiscoveryMasterFallBackTest
    {
        private const string ServiceVersion = "1.2.30.1234";
        private const string MasterEnvironment = "prod";
        private const string OriginatingEnvironment = "fake_env";
        private readonly TimeSpan _timeOut = TimeSpan.FromSeconds(5);
        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private Dictionary<string, ConsulClientMock> _consulClient;
        private IEnvironment _environment;
        private ManualConfigurationEvents _configRefresh;
        private IDateTime _dateTimeMock;
        private const int Repeat = 1;

        [SetUp]
        public void SetUp()
        {
         

            _environment = Substitute.For<IEnvironment>();
            _environment.Zone.Returns("il3");
            _environment.DeploymentEnvironment.Returns(OriginatingEnvironment);

            _configDic = new Dictionary<string, string> {{"Discovery.EnvironmentFallbackEnabled", "true"}};
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().ToConstant(_environment);

                k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                SetupConsulClientMocks();
                k.Rebind<Func<string, IConsulClient>>().ToMethod(_ => (s => _consulClient[s]));

                _dateTimeMock = Substitute.For<IDateTime>();
                _dateTimeMock.Delay(Arg.Any<TimeSpan>()).Returns(async c => await Task.Delay(c.Arg<TimeSpan>()));
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

            CreateConsulMock(MasterService(TestContext.CurrentContext.Test.Name));
            CreateConsulMock(OriginatingService(TestContext.CurrentContext.Test.Name));

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
            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());
            
            var discovery = GetServiceDiscovery();
            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());
            
            var nextHost = await GetServiceDiscovery().GetNextHost();
            nextHost.HostName.ShouldBe(MasterService());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task FallBackToMasterShouldNotHaveOriginatingServiceHealth()
        {
            var serviceName = GetServiceName();

            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());
            
            var discovery = GetServiceDiscovery();
            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());
            
            await GetServiceDiscovery().GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == MasterService()).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == OriginatingService(serviceName));
        }

        [Test]
        [Repeat(Repeat)]
        public async Task NoFallBackShouldNotHavMasterServiceHealth()
        {
            var serviceName = GetServiceName();

            SetMockToReturnServiceNotDefined(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            var discovery = GetServiceDiscovery();
            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnServiceNotDefined(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            await GetServiceDiscovery().GetNextHost();
            HealthChecks.GetStatus().Results.Single(_ => _.Name == OriginatingService()).Check.IsHealthy.ShouldBeTrue();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == MasterService(serviceName));
        }

        [Test]
        [Repeat(Repeat)]
        public void CreateServiceDiscoveryWithoutGetNextHostNoServiceHealthShouldAppear()
        {
            var serviceName = GetServiceName();

            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());
            GetServiceDiscovery();
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == MasterService(serviceName));
            HealthChecks.GetStatus().Results.ShouldNotContain(_ => _.Name == OriginatingService(serviceName));
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ScopeZoneShouldUseServiceNameAsConsoleQuery()
        {
            var serviceName = GetServiceName();

           _unitTestingKernel.Get<Func<DiscoveryConfig>>()().Services[serviceName].Scope = ServiceScope.Zone;
            SetMockToReturnHost(serviceName);
            
            var discovery = GetServiceDiscovery();
            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[serviceName].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnHost(serviceName);
            
            var nextHost = await GetServiceDiscovery().GetNextHost();
            nextHost.HostName.ShouldBe(serviceName);
        }

        [Test]
        [Retry(5)]
        public async Task WhenQueryDeleteShouldFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            var serviceName = GetServiceName();

            _configDic[$"Discovery.Services.{serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            var discovery = GetServiceDiscovery();
            

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            var waitForEvents = discovery.EndPointsChanged.WhenEventReceived(_timeOut);

            var nextHost = await discovery.GetNextHost();
            nextHost.HostName.ShouldBe(OriginatingService());

            SetMockToReturnServiceNotDefined(OriginatingService());
            await waitForEvents;

            nextHost = await discovery.GetNextHost();
            nextHost.HostName.ShouldBe(MasterService());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenQueryAddShouldNotFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            var serviceName = GetServiceName();
            _configDic[$"Discovery.Services.{serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());

            var discovery = GetServiceDiscovery();

            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnHost(MasterService());
            SetMockToReturnServiceNotDefined(OriginatingService());

            var nextHost = await discovery.GetNextHost();
            nextHost.HostName.ShouldBe(MasterService());

            var waitForEvents = discovery.EndPointsChanged.WhenEventReceived(_timeOut);
            SetMockToReturnHost(OriginatingService());
            await waitForEvents;

            nextHost = await discovery.GetNextHost();
            nextHost.HostName.ShouldBe(OriginatingService());
        }

        [Test]
        [Retry(5)]
        public void ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(MasterService());
            SetMockToReturnError(OriginatingService());
            var exception = Should.Throw<EnvironmentException>(async () => await GetServiceDiscovery().GetNextHost());
            exception.UnencryptedTags["responseLog"].ShouldBe("Error response log");
            exception.UnencryptedTags["queryDefined"].ShouldBe("True");
            exception.UnencryptedTags["consulError"].ShouldNotBeNullOrEmpty();
            exception.UnencryptedTags["requestedService"].ShouldBe(OriginatingService());

        }

        [Test]
        [Repeat(Repeat)]
        public async Task QueryDefinedShouldNotFallBackToMaster()
        {
            SetMockToReturnHost(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            var discovery = GetServiceDiscovery();

            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));

            SetMockToReturnHost(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            var nextHost = await GetServiceDiscovery().GetNextHost();
            nextHost.HostName.ShouldBe(OriginatingService());
        }

        [Test]
        [Repeat(Repeat)]
        public void MasterShouldNotFallBack()
        {
            _environment = Substitute.For<IEnvironment>();
            _environment.Zone.Returns("il3");
            _environment.DeploymentEnvironment.Returns(MasterEnvironment);
            _unitTestingKernel.Rebind<IEnvironment>().ToConstant(_environment);

            SetMockToReturnServiceNotDefined(MasterService());

            Should.Throw<EnvironmentException>(async () => await GetServiceDiscovery().GetNextHost());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task EndPointsChangedShouldNotFireWhenNothingChange()
        {
            var serviceName = GetServiceName();

            TimeSpan reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{serviceName}.ReloadInterval"] = reloadInterval.ToString();
            int numOfEvent = 0;
            SetMockToReturnHost(MasterService());
            SetMockToReturnHost(OriginatingService());

            //in the first time can fire one or two event
            var discovery = GetServiceDiscovery();

            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService()].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnHost(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            await discovery.GetNextHost();

            int events = numOfEvent;
            discovery.EndPointsChanged.LinkTo(new ActionBlock<string>(x => events++));
            
            for (int i = 0; i < 5; i++)
            {
                await discovery.GetNextHost();
            }
            events.ShouldBe(0);
        }

        [Test]
        [Retry(5)]
        public async Task EndPointsChangedShouldFireConfigChange()
        {
            var serviceName = GetServiceName();

            SetMockToReturnHost(MasterService(serviceName));
            SetMockToReturnHost(OriginatingService(serviceName));

            //in the first time can fire one or two event
            var discovery = GetServiceDiscovery(serviceName);
            

            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService(serviceName)].InitFinished.Task,
                _consulClient[OriginatingService()].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            
            SetMockToReturnHost(MasterService());
            SetMockToReturnHost(OriginatingService());
            
            var waitForEvents = discovery.EndPointsChanged.StartCountingEvents();

            await discovery.GetNextHost();

            _configDic[$"Discovery.Services.{serviceName}.Hosts"] = "localhost";
            _configDic[$"Discovery.Services.{serviceName}.Source"] = "Config";

            Task waitForChangeEvent = waitForEvents.WhenNextEventReceived();
            await _configRefresh.ApplyChanges<DiscoveryConfig>();
            await waitForChangeEvent;
            var host = await discovery.GetNextHost();
            host.HostName.ShouldBe("localhost");
            waitForEvents.ReceivedEvents.Count.ShouldBe(1);
        }

        public string GetServiceName([CallerMemberName] string caller = null)
        {
            return caller;
        }
        
        [Test]
        [Retry(5)]
        public async Task GetAllEndPointsChangedShouldFireConfigChange()
        {
            var serviceName = GetServiceName();

            SetMockToReturnHost(MasterService(serviceName));
            SetMockToReturnHost(OriginatingService(serviceName));

            //in the first time can fire one or two event
            var discovery = GetServiceDiscovery(serviceName);

            //wait for discovery to be initialize!!
            var endPoints = await discovery.GetAllEndPoints();
            endPoints.Single().HostName.ShouldBe(OriginatingService(serviceName));

            var waitForEvents = discovery.EndPointsChanged.StartCountingEvents();

            _configDic[$"Discovery.Services.{serviceName}.Source"] = "Config";
            _configDic[$"Discovery.Services.{serviceName}.Hosts"] = "localhost";
            Console.WriteLine("RaiseChangeEvent");

            Task waitForChangeEvent = waitForEvents.WhenNextEventReceived();
            _configRefresh.RaiseChangeEvent();
            await waitForChangeEvent;
            waitForEvents.ReceivedEvents.Count.ShouldBe(1);


            endPoints = await discovery.GetAllEndPoints();
            endPoints.Single().HostName.ShouldBe("localhost");
            waitForEvents.ReceivedEvents.Count.ShouldBe(1);
        }

        [Test]
        [Retry(5)]
        public async Task EndPointsChangedShouldFireWhenHostChange()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            var serviceName = GetServiceName();
            _configDic[$"Discovery.Services.{serviceName}.ReloadInterval"] = reloadInterval.ToString();
            SetMockToReturnHost(MasterService(serviceName));
            SetMockToReturnHost(OriginatingService(serviceName));
            var discovery = GetServiceDiscovery(serviceName);
            await discovery.GetAllEndPoints();

            var timeout = Task.Delay(TimeSpan.FromSeconds(10));
            var initsFinished = Task.WhenAny(_consulClient[MasterService(serviceName)].InitFinished.Task,
                _consulClient[OriginatingService(serviceName)].InitFinished.Task);
            
            Assert.AreNotEqual(timeout, await Task.WhenAny(timeout, initsFinished));
            

            var wait = discovery.EndPointsChanged.StartCountingEvents();
            bool UseOriginatingService(int i) => i % 2 == 0;
            for (int i = 1; i < 6; i++)
            {
                var waitForNextEvent = wait.WhenNextEventReceived();
                //act                
                if (UseOriginatingService(i))
                    SetMockToReturnHost(OriginatingService(serviceName));
                else
                    SetMockToReturnServiceNotDefined(OriginatingService(serviceName));

                await waitForNextEvent;
                //assert
                wait.ReceivedEvents.Count.ShouldBe(i);
                var nextHost = (await discovery.GetNextHost()).HostName;
                if (UseOriginatingService(i))
                    nextHost.ShouldBe(OriginatingService(serviceName));
                else
                    nextHost.ShouldBe(MasterService(serviceName));
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
            if (!_consulClient.ContainsKey(serviceName))
                CreateConsulMock(serviceName);
            
            _consulClient[serviceName].SetResult(new EndPointsResult {IsQueryDefined = false});            
        }

        private void SetMockToReturnError(string serviceName)
        {
            if (!_consulClient.ContainsKey(serviceName))
                CreateConsulMock(serviceName);
            
            _consulClient[serviceName].SetResult(
                new EndPointsResult
                {
                    Error = new EnvironmentException("Mock: some error"),
                    IsQueryDefined = true,
                    ResponseLog = "Error response log"
                });
        }

        [Test]
        public void ServiceDiscoverySameNameShouldBeTheSame()
        {
            var serviceName = GetServiceName();
            Assert.AreEqual(GetServiceDiscovery(serviceName), GetServiceDiscovery(serviceName));
        }

        private readonly ReachabilityChecker _reachabilityChecker = x => Task.FromResult(true);

        private IServiceDiscovery GetServiceDiscovery([CallerMemberName]string serviceName = null)
        {
            var discovery =
                _unitTestingKernel.Get<Func<string, ReachabilityChecker, IServiceDiscovery>>()(serviceName,
                    _reachabilityChecker);
            Task.Delay(200).GetAwaiter()
                .GetResult(); // let ConsulClient return the expected result before getting the dicovery object
            return discovery;
        }


        private string MasterService([CallerMemberName]string serviceName= null) => ConsulServiceName(serviceName, MasterEnvironment);
        private string OriginatingService([CallerMemberName]string serviceName = null) => ConsulServiceName(serviceName, OriginatingEnvironment);

        private static string ConsulServiceName(string serviceName, string deploymentEnvironment) =>
            $"{serviceName}-{deploymentEnvironment}";
    }
}