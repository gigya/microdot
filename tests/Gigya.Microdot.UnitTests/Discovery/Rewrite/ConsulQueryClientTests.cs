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
using IConsulClient = Gigya.Microdot.ServiceDiscovery.Rewrite.IConsulClient;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class ConsulQueryClientTests
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
                k.Rebind<IConsulClient>().To<ConsulQueryClient>().InTransientScope();    // for testing - use a new instance for each test
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

        public async Task LoadNodes()
        {
            await Task.WhenAny(_consulClient.LoadNodes(_serviceState), Task.Delay(5000)).ConfigureAwait(false);
        }

        [Test]
        public async Task NodeExists()
        {           
            AddServiceEndPoint();

            await LoadNodes();

            AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeAdded()
        {
            await LoadNodes();
            AddServiceEndPoint();
            await LoadNodes();

            AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeRemoved()
        {
            AddServiceEndPoint();
            AddServiceEndPoint("endpointToRemove");

            await LoadNodes();

            _serviceState.Nodes.Length.ShouldBe(2);
            
            RemoveServiceEndPoint("endpointToRemove");
            await LoadNodes();

            AssertOneDefaultNode();
        }

        [Test]
        public async Task StartWithError()
        {
            AddServiceEndPoint();
            SetConsulIsDown();

            await LoadNodes();
            _serviceState.LastResult.Error.ShouldNotBeNull();
        }

        [Test]
        public async Task ServiceMissingOnStart()
        {
            await LoadNodes();
            _serviceState.IsDeployed.ShouldBeFalse();
            _serviceState.LastResult.Error.ShouldBeNull();
        }

        [Test]
        public async Task ServiceBecomesMissing()
        {
            AddServiceEndPoint();
            await LoadNodes();

            _serviceState.IsDeployed.ShouldBeTrue();

            RemoveService();
            await LoadNodes();

            _serviceState.IsDeployed.ShouldBeFalse();
            _serviceState.LastResult.Error.ShouldBeNull();
        }

        [Test]
        public async Task ServiceIsBackAfterBeingMissing()
        {
            await LoadNodes();

            _serviceState.IsDeployed.ShouldBeFalse();
            await LoadNodes();

            AddServiceEndPoint();
            await LoadNodes();

            AssertOneDefaultNode();
        }


        [Test]
        public async Task ServiceIsDeployedWithNoNodes()
        {
            SetServiceVersion("1.0.0");
            await LoadNodes();
            _serviceState.IsDeployed.ShouldBeTrue();
            _serviceState.Nodes.Length.ShouldBe(0);

            var delays = _dateTimeFake.DelaysRequested.ToArray();
            delays.Length.ShouldBeLessThan(4); // shouldn't take too many loops to get the result
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

        private void RemoveService()
        {
            _consulSimulator.RemoveService(_serviceName);
        }

    }
}
