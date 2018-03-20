using System;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
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

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class QueryBasedConsulNodeMonitorTests
    {
        private const string ServiceName = "MyService-prod";
        private const int ConsulPort = 8508;
        private const string DataCenter = "us1";

        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private const string Host2 = "Host2";

        private TestingKernel<ConsoleLog> _testingKernel;
        private IQueryBasedConsulNodeMonitor _consulQueryNodeMonitor;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ConsulSimulator _consulSimulator;
        private string _serviceName;
        private DateTimeFake _dateTimeFake;
        private ConsulConfig _consulConfig;        

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
                _environmentVariableProvider.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                _environmentVariableProvider.DataCenter.Returns(DataCenter);
                k.Rebind<IEnvironmentVariableProvider>().ToMethod(_ => _environmentVariableProvider);

                k.Rebind<IDateTime>().ToMethod(_ => _dateTimeFake);

                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => ()=>_consulConfig);                
            });

        }

        [OneTimeTearDown]
        public void TearDownConsulListener()
        {
            _testingKernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _consulSimulator = new ConsulSimulator(ConsulPort);

            _serviceName = $"{ServiceName}_{Guid.NewGuid()}-prod";

            _dateTimeFake = new DateTimeFake(false);
            _consulConfig = new ConsulConfig{ReloadInterval = TimeSpan.FromMilliseconds(100)};

            _consulQueryNodeMonitor = _testingKernel.Get<Func<string,IQueryBasedConsulNodeMonitor>>()(_serviceName);
        }

        [TearDown]
        public void Teardown()
        {
            _consulSimulator.Dispose();
            _consulQueryNodeMonitor.Dispose();
        }

        public async Task WaitForUpdates()
        {
            await Task.Delay(300).ConfigureAwait(false);
        }

        [Test]
        public async Task ServiceExists()
        {           
            AddServiceNode();

            await Init();

            AssertOneDefaultNode();
        }

        [Test]
        public async Task ServiceAdded()
        {
            await Init();
            AddServiceNode();
            await WaitForUpdates();

            AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeAdded()
        {
            SetServiceVersion(Version);
            AddServiceNode();
            await Init();
            AssertOneDefaultNode();

            AddServiceNode(Host2);
            await WaitForUpdates();
            _consulQueryNodeMonitor.Nodes.Length.ShouldBe(2);
            _consulQueryNodeMonitor.Nodes[1].Hostname.ShouldBe(Host2);
        }

        [Test]
        public async Task NodeRemoved()
        {
            AddServiceNode();
            AddServiceNode("endpointToRemove");

            await Init();

            _consulQueryNodeMonitor.Nodes.Length.ShouldBe(2);
            
            RemoveServiceNode("endpointToRemove");
            await WaitForUpdates();

            AssertOneDefaultNode();
        }


        [Test]
        public async Task StartWithError()
        {
            AddServiceNode();
            SetConsulIsDown();

            await Init();

            ShouldThrowExceptionWhenRequestingNodes();
        }

        [Test]
        public async Task ErrorWhileRunning_KeepLastKnownResult()
        {
            AddServiceNode();
            await Init();

            SetConsulIsDown();
            await WaitForUpdates();

            AssertOneDefaultNode();
        }

        [Test]
        public async Task ServiceMissingOnStart()
        {
            await Init();
            _consulQueryNodeMonitor.IsDeployed.ShouldBeFalse();
        }

        [Test]
        public async Task ServiceBecomesMissing()
        {
            AddServiceNode();
            await Init();

            _consulQueryNodeMonitor.IsDeployed.ShouldBeTrue();

            RemoveService();
            await WaitForUpdates();

            _consulQueryNodeMonitor.IsDeployed.ShouldBeFalse();
        }

        [Test]        
        public async Task ServiceIsDeployedWithNoNodes()
        {
            AddServiceNode();
            RemoveServiceNode();
            await Init();
            _consulQueryNodeMonitor.IsDeployed.ShouldBeTrue();
            ShouldThrowExceptionWhenRequestingNodes();
        }


        [Test]
        public async Task ServiceIsBackAfterBeingMissing()
        {
            await Init();

            _consulQueryNodeMonitor.IsDeployed.ShouldBeFalse();
            await WaitForUpdates();

            AddServiceNode();
            await WaitForUpdates();

            AssertOneDefaultNode();
        }

        private Task Init()
        {
            _consulQueryNodeMonitor = _testingKernel.Get<Func<string, IQueryBasedConsulNodeMonitor>>()(_serviceName);
            return _consulQueryNodeMonitor.Init();
        }

        private void AssertOneDefaultNode()
        {
            _consulQueryNodeMonitor.IsDeployed.ShouldBeTrue();
            _consulQueryNodeMonitor.Nodes.Length.ShouldBe(1);
            _consulQueryNodeMonitor.Nodes[0].Hostname.ShouldBe(Host1);
            _consulQueryNodeMonitor.Nodes[0].Port.ShouldBe(Port1);
        }

        private void ShouldThrowExceptionWhenRequestingNodes()
        {
            var getNodesAction = (Action)(() =>
            {
                var _ = _consulQueryNodeMonitor.Nodes;
            });

            getNodesAction.ShouldThrow<EnvironmentException>();
        }

        private async void AddServiceNode(string hostName=Host1, int port=Port1, string version=Version, string serviceName=null)
        {            
            _consulSimulator.AddServiceNode(serviceName??_serviceName, new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceNode(string hostName = Host1, int port = Port1, string serviceName=null)
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
