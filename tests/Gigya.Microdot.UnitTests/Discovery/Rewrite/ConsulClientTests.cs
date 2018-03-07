using System;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using ConsulClient = Gigya.Microdot.ServiceDiscovery.Rewrite.ConsulClient;
using IConsulClient = Gigya.Microdot.ServiceDiscovery.Rewrite.IConsulClient;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class ConsulClientTests
    {
        private const string ServiceName = "MyService-prod";
        private const int ConsulPort = 8501;
        private const string DataCenter = "us1";

        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private TestingKernel<ConsoleLog> _testingKernel;
        private IConsulClient _consulClient;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ConsulSimulator _consulSimulator;
        private string _serviceName;
        private DateTimeFake _dateTimeFake;
        private ConsulConfig _consulConfig;
        private ConsulServiceState _serviceState;
        public enum ConsulMethod { LongPolling, Queries }

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _consulSimulator = new ConsulSimulator(ConsulPort);

            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
                _environmentVariableProvider.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                _environmentVariableProvider.DataCenter.Returns(DataCenter);
                k.Rebind<IEnvironmentVariableProvider>().ToMethod(_ => _environmentVariableProvider);

                k.Rebind<IDateTime>().ToMethod(_ => _dateTimeFake);

                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => ()=>_consulConfig);
                k.Rebind<IConsulClient>().To<ConsulClient>().InTransientScope();    // for testing - use a new instance for each test
            });

        }

        [OneTimeTearDown]
        public void TearDownConsulListener()
        {
            _consulSimulator.Dispose();
            _testingKernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _serviceName = ServiceName + "_" + Guid.NewGuid();
            _dateTimeFake = new DateTimeFake(false);
            _consulConfig = new ConsulConfig();

            _serviceState = new ConsulServiceState(_serviceName);
            _consulClient = _testingKernel.Get<IConsulClient>();

            _consulSimulator.Reset();            
        }

        [TearDown]
        public void Teardown()
        {
            _consulClient.Dispose();
        }

        public async Task LoadNodes(ConsulMethod consulMethod)
        {
            switch (consulMethod)
            {
                case ConsulMethod.LongPolling:
                    await Task.WhenAny(_consulClient.LoadNodes(_serviceState), Task.Delay(5000)).ConfigureAwait(false);
                    break;
                case ConsulMethod.Queries:
                    await Task.WhenAny(_consulClient.LoadNodesByQuery(_serviceState), Task.Delay(5000)).ConfigureAwait(false);
                    break;
            }
        }

        [Test]
        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task NodeExists(ConsulMethod consulMethod)
        {           
            AddServiceEndPoint();

            await LoadNodes(consulMethod);

            AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeAdded_LongPolling()
        {
            SetServiceVersion(Version);
            await LoadNodes(ConsulMethod.LongPolling);
            AddServiceEndPoint();
            await LoadNodes(ConsulMethod.LongPolling);            

            AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeAdded_Query()
        {
            await LoadNodes(ConsulMethod.Queries);
            AddServiceEndPoint();
            await LoadNodes(ConsulMethod.Queries);

            AssertOneDefaultNode();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task NodeRemoved(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();
            AddServiceEndPoint("endpointToRemove");

            await LoadNodes(consulMethod);

            _serviceState.Nodes.Length.ShouldBe(2);
            
            RemoveServiceEndPoint("endpointToRemove");
            await LoadNodes(consulMethod);

            AssertOneDefaultNode();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task StartWithError(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();
            SetConsulIsDown();

            await LoadNodes(consulMethod);
            _serviceState.LastResult.Error.ShouldNotBeNull();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task ErrorAfterStart_UseLastKnownNodes(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();

            await LoadNodes(ConsulMethod.LongPolling);
            var nodesBeforeError = _serviceState.Nodes;

            SetConsulIsDown();            
            AddServiceEndPoint("another host");
            await LoadNodes(ConsulMethod.LongPolling);

            var nodesAfterError = _serviceState.Nodes;
            nodesAfterError.ShouldBe(nodesBeforeError);

            SetConsulIsUpAgain();
            await LoadNodes(ConsulMethod.LongPolling);

            _serviceState.LastResult.Error.ShouldBeNull();
            _serviceState.Nodes.Length.ShouldBe(2);
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task ServiceMissingOnStart(ConsulMethod consulMethod)
        {
            await LoadNodes(consulMethod);
            _serviceState.IsDeployed.ShouldBeFalse();
            _serviceState.LastResult.Error.ShouldBeNull();
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]        
        public async Task ServiceBecomesMissing(ConsulMethod consulMethod)
        {
            AddServiceEndPoint();
            await LoadNodes(consulMethod);

            _serviceState.IsDeployed.ShouldBeTrue();

            RemoveService();
            await LoadNodes(consulMethod);

            _serviceState.IsDeployed.ShouldBeFalse();
            _serviceState.LastResult.Error.ShouldBeNull();
        }
        
        [TestCase(ConsulMethod.Queries)]        
        public async Task ServiceIsBackAfterBeingMissing(ConsulMethod consulMethod)
        {
            await LoadNodes(consulMethod);

            _serviceState.IsDeployed.ShouldBeFalse();
            await LoadNodes(consulMethod);

            AddServiceEndPoint();
            await LoadNodes(consulMethod);

            AssertOneDefaultNode();
        }


        [Test]
        public async Task UpgradeVersion()
        {
            AddServiceEndPoint(hostName: "oldVersionHost", version: "1.0.0");
            AddServiceEndPoint(hostName: "newVersionHost", version: "2.0.0");
            SetServiceVersion("1.0.0");

            await LoadNodes(ConsulMethod.LongPolling);
            
            _serviceState.Nodes.Length.ShouldBe(1);
            _serviceState.Nodes[0].Hostname.ShouldBe("oldVersionHost");
            _serviceState.ActiveVersion.ShouldBe("1.0.0");

            SetServiceVersion("2.0.0");
            await LoadNodes(ConsulMethod.LongPolling);

            _serviceState.Nodes.Length.ShouldBe(1);
            _serviceState.Nodes[0].Hostname.ShouldBe("newVersionHost");
            _serviceState.ActiveVersion.ShouldBe("2.0.0");
        }

        [TestCase(ConsulMethod.LongPolling)]
        [TestCase(ConsulMethod.Queries)]
        public async Task ServiceIsDeployedWithNoNodes(ConsulMethod consulMethod)
        {
            SetServiceVersion("1.0.0");
            await LoadNodes(consulMethod);
            _serviceState.IsDeployed.ShouldBeTrue();
            _serviceState.Nodes.Length.ShouldBe(0);

            var delays = _dateTimeFake.DelaysRequested.ToArray();
            delays.Length.ShouldBeLessThan(4); // shouldn't take too many loops to get the result
        }

        [TestCase(ConsulMethod.LongPolling)]                
        public async Task ServiceIsDeployedInLowerCase(ConsulMethod consulMethod)
        {
            AddServiceEndPoint(serviceName: _serviceName.ToLower());            
            await LoadNodes(consulMethod);
            
            AssertOneDefaultNode();
        }

        private void AssertOneDefaultNode()
        {
            _serviceState.Nodes.Length.ShouldBe(1);
            _serviceState.Nodes[0].Hostname.ShouldBe(Host1);
            _serviceState.Nodes[0].Port.ShouldBe(Port1);
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

    }
}
