using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class ConsulQueryNodeSourceTests
    {
        private const string ServiceName = "MyService-prod";
        private const int ConsulPort = 8508;
        private const string DataCenter = "us1";

        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private const string Host2 = "Host2";

        private TestingKernel<ConsoleLog> _testingKernel;
        private ConsulQueryNodeSource _nodeSource;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ConsulSimulator _consulSimulator;
        private DeploymentIdentifier _deploymentIdentifier;
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

            _deploymentIdentifier = new DeploymentIdentifier($"{ServiceName}_{Guid.NewGuid()}", "prod");

            _dateTimeFake = new DateTimeFake(false);
            _consulConfig = new ConsulConfig{ReloadInterval = TimeSpan.FromMilliseconds(100)};

            CreateNodeSource();
        }

        [TearDown]
        public async Task Teardown()
        {
            _consulSimulator?.Dispose();
            _nodeSource?.Shutdown();
        }

        public async Task WaitForUpdates()
        {
            await Task.Delay(1500).ConfigureAwait(false);
        }

        [Test]
        public async Task ServiceExists()
        {           
            AddServiceNode();

            await Init();

            await AssertOneDefaultNode();
        }

        [Test]
        public async Task ServiceNotExists()
        {
            await Init();
            _nodeSource.WasUndeployed.ShouldBeTrue();
        }


        [Test]
        public async Task ServiceAdded()
        {            
            await Init();
            AddServiceNode();
            await WaitForUpdates();

            await AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeAdded()
        {
            SetServiceVersion(Version);
            AddServiceNode();
            await Init();
            await AssertOneDefaultNode();

            AddServiceNode(Host2);
            await WaitForUpdates();
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(2);
            nodes[1].Hostname.ShouldBe(Host2);
        }

        [Test]
        public async Task NodeRemoved()
        {
            AddServiceNode();
            AddServiceNode("endpointToRemove");

            await Init();

            _nodeSource.GetNodes().Length.ShouldBe(2);
            
            RemoveServiceNode("endpointToRemove");
            await WaitForUpdates();

            await AssertOneDefaultNode();
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

            await AssertOneDefaultNode();
        }

        [Test]
        public async Task ServiceMissingOnStart()
        {
            await Init();
            _nodeSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceRemoved()
        {
            AddServiceNode();
            await Init();

            _nodeSource.WasUndeployed.ShouldBeFalse();

            RemoveService();
            await WaitForUpdates();

            _nodeSource.WasUndeployed.ShouldBeTrue();

            // WasUndeployed stays true even after it was re-deployed. The source needs to be re-created to keep monitoring again
            AddServiceNode();
            await WaitForUpdates();
            _nodeSource.WasUndeployed.ShouldBeTrue();

        }

        [Test]        
        public async Task ServiceIsDeployedWithNoNodes()
        {
            AddServiceNode();
            RemoveServiceNode();
            await Init();
            _nodeSource.WasUndeployed.ShouldBeFalse();
            ShouldThrowExceptionWhenRequestingNodes();
        }


        [Test]
        public async Task ServiceIsBackAfterBeingMissing()
        {
            await Init();

            _nodeSource.WasUndeployed.ShouldBeTrue();
            await WaitForUpdates();

            AddServiceNode();
            await WaitForUpdates();

            await AssertOneDefaultNode();
        }

        [Test]
        public void SupportMultipleEnvironments()
        {
            CreateNodeSource();
            _nodeSource.SupportsMultipleEnvironments.ShouldBeTrue();
        }


        private Task Init()
        {
            CreateNodeSource();
            return _nodeSource.Init();
        }

        private void CreateNodeSource()
        {
            _nodeSource = _testingKernel.Get<Func<DeploymentIdentifier, ConsulQueryNodeSource>>()(_deploymentIdentifier);            
        }

        private async Task AssertOneDefaultNode()
        {
            await ReloadNodeMonitorIfNeeded();
            _nodeSource.WasUndeployed.ShouldBeFalse();
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe(Host1);
            nodes[0].Port.ShouldBe(Port1);
        }

        private async Task ReloadNodeMonitorIfNeeded()
        {
            if (_nodeSource.WasUndeployed)
            {
                _nodeSource.Shutdown();
                await Init();
            }
        }
        private void ShouldThrowExceptionWhenRequestingNodes()
        {
            var getNodesAction = (Action)(() =>
            {
                _nodeSource.GetNodes();
            });

            getNodesAction.ShouldThrow<EnvironmentException>();
        }

        private async void AddServiceNode(string hostName=Host1, int port=Port1, string version=Version)
        {            
            _consulSimulator.AddServiceNode(_deploymentIdentifier.ToString(), new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceNode(string hostName = Host1, int port = Port1)
        {
            _consulSimulator.RemoveServiceNode(_deploymentIdentifier.ToString(), new ConsulEndPoint { HostName = hostName, Port = port});
        }

        private void SetServiceVersion(string version)
        {
            _consulSimulator.SetServiceVersion(_deploymentIdentifier.ToString(), version);
        }

        private void SetConsulIsDown()
        {
            _consulSimulator.SetError(new Exception("fake error"));
        }

        private void RemoveService()
        {
            _consulSimulator.RemoveService(_deploymentIdentifier.ToString());
        }

    }
}
