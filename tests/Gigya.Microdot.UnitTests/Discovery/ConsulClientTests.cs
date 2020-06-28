using System;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using Gigya.Microdot.Testing.Shared.Utils;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ConsulClientTests
    {
        private const string ServiceName = "MyService-prod";
        private  int ConsulPort = DisposablePort.GetPort().Port;
        private const string Zone = "us1a";

        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        public enum ConsulMethod { LongPolling, Queries}

        private TestingKernel<ConsoleLog> _testingKernel;
        private IConsulClient _consulClient;
        private IEnvironment _environment;
        private ConsulSimulator _consulSimulator;
        private string _serviceName;
        private DateTimeFake _dateTimeFake;
        private ConsulConfig _consulConfig;

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _consulSimulator = new ConsulSimulator(ConsulPort);

            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                _environment = Substitute.For<IEnvironment>();
                _environment.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                _environment.Zone.Returns(Zone);
                k.Rebind<IEnvironment>().ToMethod(_ => _environment);

                k.Rebind<IDateTime>().ToMethod(_ => _dateTimeFake);
            });

        }

        [OneTimeTearDown]
        public void TearDownConsulListener()
        {
            _testingKernel.Dispose();
            _consulSimulator.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _consulSimulator.Reset();
            _serviceName = ServiceName + "_" + Guid.NewGuid();
            _dateTimeFake = new DateTimeFake(false);
            _consulConfig = _testingKernel.Get<Func<ConsulConfig>>()();
        }

        private Task<EndPointsResult> Start(ConsulMethod consulMethod)
        {
            _consulConfig.LongPolling = (consulMethod == ConsulMethod.LongPolling);
            _consulClient = _testingKernel.Get<Func<string, IConsulClient>>()(_serviceName);
            return GetResultAfter(() => _consulClient.Init());
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task EndpointExists(ConsulMethod consulMethod)
        {           
            AddServiceEndPoint();

            var result = await Start(consulMethod);

            AssertOneDefaultEndpoint(result);
        }

        [Test]       
        public async Task EndpointAdded_LongPolling()
        {
            await Start(ConsulMethod.LongPolling);
            var result = await GetResultAfter(() => AddServiceEndPoint());

            AssertOneDefaultEndpoint(result);
        }

        [Test]
        public async Task EndpointAdded_Query()
        {
            await Start(ConsulMethod.Queries);

            var result = await GetResultAfter(()=>AddServiceEndPoint());

            AssertOneDefaultEndpoint(result);
            var delays = _dateTimeFake.DelaysRequested.ToArray();
            delays.Length.ShouldBeLessThan(4); // shouldn't take too many loops to get the result
            delays.ShouldAllBe(d => d.Equals(_consulConfig.ReloadInterval));
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task EndpointRemoved(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();
            AddServiceEndPoint("endpointToRemove");

            var result = await Start(consulMethod);

            result.EndPoints.Length.ShouldBe(2);
            
            result = await GetResultAfter(()=>RemoveServiceEndPoint("endpointToRemove"));            
            AssertOneDefaultEndpoint(result);
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task StartWithError(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();
            SetConsulIsDown();

            var result = await Start(consulMethod);
            result.Error.ShouldNotBeNull();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task ErrorAfterStart_UseLastKnownEndpoints(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();

            var resultBeforeError = await Start(ConsulMethod.LongPolling);

            SetConsulIsDown();
            AddServiceEndPoint("another host");

            await Task.Delay(1000);
            var resultAfterError = _consulClient.Result;
            resultAfterError.EndPoints.ShouldBe(resultBeforeError.EndPoints);

            var result = await GetResultAfter(SetConsulIsUpAgain);
            result.Error.ShouldBeNull();
            result.EndPoints.Length.ShouldBe(2);
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task ServiceMissingOnStart(ConsulMethod consulMethod)
        {
            var result = await Start(consulMethod);
            result.IsQueryDefined.ShouldBeFalse();
            result.Error.ShouldBeNull();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]        
        public async Task ServiceBecomesMissing(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();
            var result = await Start(consulMethod);

            result.IsQueryDefined.ShouldBeTrue();

            result = await GetResultAfter(()=>RemoveService());
            result.IsQueryDefined.ShouldBeFalse();
            result.Error.ShouldBeNull();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]        
        public async Task ServiceIsBackAfterBeingMissing(ConsulMethod consulMethod)
        {
            var result = await Start(consulMethod);

            result.IsQueryDefined.ShouldBeFalse();

            result = await GetResultAfter(()=>AddServiceEndPoint());
            AssertOneDefaultEndpoint(result);
        }


        [Test]
        public async Task UpgradeVersion()
        {
            AddServiceEndPoint(hostName: "oldVersionHost", version: "1.0.0");
            AddServiceEndPoint(hostName: "newVersionHost", version: "2.0.0");
            SetServiceVersion("1.0.0");

            var result = await Start(ConsulMethod.LongPolling);
            
            result.EndPoints.Length.ShouldBe(1);
            result.EndPoints[0].HostName.ShouldBe("oldVersionHost");
            result.ActiveVersion.ShouldBe("1.0.0");

            result = await GetResultAfter(()=> SetServiceVersion("2.0.0"));
            result.EndPoints.Length.ShouldBe(1);
            result.EndPoints[0].HostName.ShouldBe("newVersionHost");
            result.ActiveVersion.ShouldBe("2.0.0");
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task ServiceIsDeployedWithNoNodes(ConsulMethod consulMethod)
        {
            SetServiceVersion("1.0.0");
            var result = await Start(consulMethod);            
            result.IsQueryDefined.ShouldBeTrue();
            result.EndPoints.Length.ShouldBe(0);

            var delays = _dateTimeFake.DelaysRequested.ToArray();
            delays.Length.ShouldBeLessThan(4); // shouldn't take too many loops to get the result
        }

        [TestCase(ConsulMethod.LongPolling)]        
        public async Task ServiceIsDeployedInLowerCase(ConsulMethod consulMethod)
        {
            AddServiceEndPoint(serviceName: _serviceName.ToLower());
            var result = await Start(consulMethod);
            AssertOneDefaultEndpoint(result);
        }

        private static void AssertOneDefaultEndpoint(EndPointsResult result)
        {
            result.EndPoints.Length.ShouldBe(1);
            result.EndPoints[0].HostName.ShouldBe(Host1);
            result.EndPoints[0].Port.ShouldBe(Port1);
            result.ActiveVersion.ShouldBe(Version);
        }

        private async void AddServiceEndPoint(string hostName=Host1, int port=Port1, string version=Version, string serviceName=null)
        {            
            _consulSimulator.AddServiceNode(serviceName??_serviceName, new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceEndPoint(string hostName = Host1, int port = Port1, string serviceName=null)
        {
            _consulSimulator.RemoveServiceNode(serviceName??_serviceName, new ConsulEndPoint { HostName = hostName, Port = port});
        }

        private void SetServiceVersion(string version, string serviceName=null)
        {
            _consulSimulator.SetServiceVersion(serviceName??_serviceName, version);
        }

        private void SetConsulIsDown()
        {
            _consulSimulator.SetError(new Exception("fake error"));
        }

        private void SetConsulIsUpAgain()
        {
            _consulSimulator.SetError(null);
        }

        private void RemoveService()
        {
            _consulSimulator.RemoveService(_serviceName);
        }

        private async Task<EndPointsResult> GetResultAfter(Action doSomethingToCauseResultChange)
        {            
            var waitForEvent = _consulClient.ResultChanged.WhenEventReceived(TimeSpan.FromDays(1));
            doSomethingToCauseResultChange();
            return await waitForEvent;
        }

        private Task<EndPointsResult> GetResult() => GetResultAfter(() => { });

    }
}
