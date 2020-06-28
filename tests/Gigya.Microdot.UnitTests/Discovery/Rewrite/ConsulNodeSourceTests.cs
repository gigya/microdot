using System;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using Metrics;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ConsulNodeSourceTests
    {
        private  int ConsulPort = DisposablePort.GetPort().Port;
        private const string Zone = "us1a";

        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private const string Host2 = "Host2";
        private const int Port2 = 5678;
        private const string Version2 = "2.0.0.1";

        private TestingKernel<ConsoleLog> _testingKernel;
        private INodeSource _nodeSource;
        private IEnvironment _environment;
        private ConsulSimulator _consulSimulator;
        private DeploymentIdentifier _deploymentIdentifier;
        private ConsulConfig _consulConfig;

        private string _serviceName;
        private ConsulNodeSourceFactory _consulNodeSourceFactory;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _consulSimulator = new ConsulSimulator(ConsulPort);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _consulSimulator.Dispose();
            _testingKernel.Dispose();
        }

        [SetUp]
        public async Task Setup()
        {
            _consulSimulator.Reset();
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                _environment = Substitute.For<IEnvironment>();
                _environment.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                _environment.Zone.Returns(Zone);
                k.Rebind<IEnvironment>().ToMethod(_ => _environment);
                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => () => _consulConfig);
                k.Rebind<ConsulNodeSourceFactory>().ToSelf().InTransientScope();
            });
            _serviceName = $"MyService_{Guid.NewGuid().ToString().Substring(5)}";            

            _deploymentIdentifier = new DeploymentIdentifier(_serviceName, "prod", Substitute.For<IEnvironment>());
            _consulConfig = new ConsulConfig {ErrorRetryInterval = TimeSpan.FromMilliseconds(10)};
            _consulNodeSourceFactory = _testingKernel.Get<ConsulNodeSourceFactory>();
        }

        [TearDown]
        public async Task Teardown()
        {
            _nodeSource?.Dispose();
            _testingKernel?.Dispose();
            _consulNodeSourceFactory?.Dispose();
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
            GetHealthStatus().IsHealthy.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceNotExists()
        {
            await Init();
            _nodeSource.ShouldBeNull();
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
            AddServiceNode("nodeToRemove");

            await Init();
            
            _nodeSource.GetNodes().Length.ShouldBe(2);

            RemoveServiceEndPoint("nodeToRemove");
            await WaitForUpdates();

            await AssertOneDefaultNode();
        }


        [Test]
        public async Task ServiceVersionHasChanged()
        {
            AddServiceNode();
            AddServiceNode(Host2, Port2, Version2);
            await Init();
            await AssertOneDefaultNode();

            SetServiceVersion(Version2);
            await WaitForUpdates();
            
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe(Host2);
            nodes[0].Port.ShouldBe(Port2);
            GetHealthStatus().IsHealthy.ShouldBeTrue();
        }

        [Test]
        public async Task ErrorWhileRunning_KeepLastKnownResult()
        {
            AddServiceNode();
            await Init();

            SetConsulIsDown();
            await WaitForUpdates();
            
            await AssertOneDefaultNode();

            GetHealthStatus().IsHealthy.ShouldBeFalse();
        }


        [Test]
        public async Task ErrorOnStart()
        {
            SetConsulIsDown();
            Init().ShouldThrow<EnvironmentException>();
        }

        [Test]
        public async Task UpgradeVersion()
        {
            AddServiceNode(hostName: "oldVersionHost", version: "1.0.0");
            AddServiceNode(hostName: "newVersionHost", version: "2.0.0");
            SetServiceVersion("1.0.0");

            await Init();
            
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe("oldVersionHost");

            SetServiceVersion("2.0.0");
            await WaitForUpdates();
            
            nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe("newVersionHost");
            GetHealthStatus().IsHealthy.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceIsDeployedWithNoNodes_ThrowsEnvironmentException()
        {
            SetServiceVersion("1.0.0");
            await Init();
            AssertExceptionIsThrown();
            GetHealthStatus().IsHealthy.ShouldBeFalse();
        }

        [Test]
        public async Task Disposed_StopMonitoring()
        {
            AddServiceNode();
            await Init();
            
            await WaitForUpdates();
            var healthRequestsCounterBeforeDisposed = _consulSimulator.HealthRequestsCounter;
            _nodeSource.Dispose();

            _consulSimulator.HealthRequestsCounter.ShouldBe(healthRequestsCounterBeforeDisposed, "service monitoring should have been stopped when the service became undeployed");
            GetHealthStatus().IsHealthy.ShouldBeTrue();
        }

        private async Task Init()
        {
            await WaitForUpdates();
            _nodeSource = await _consulNodeSourceFactory.CreateNodeSource(_deploymentIdentifier);            
        }


        private async Task AssertOneDefaultNode()
        {
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe(Host1);
            nodes[0].Port.ShouldBe(Port1);
        }

        private void AssertExceptionIsThrown()
        {
            var getNodesAction = (Action) (() =>
            {
                _nodeSource.GetNodes();
            });

            getNodesAction.ShouldThrow<EnvironmentException>();
        }

        private HealthCheckResult GetHealthStatus()
        {
            var healthMonitor = (FakeHealthMonitor)_testingKernel.Get<IHealthMonitor>();
            return healthMonitor.Monitors["Consul"]();
        }

        private async void AddServiceNode(string hostName=Host1, int port=Port1, string version=Version, string serviceName=null)
        {            
            _consulSimulator.AddServiceNode(serviceName ?? _deploymentIdentifier.GetConsulServiceName(), new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceEndPoint(string hostName = Host1, int port = Port1)
        {
            _consulSimulator.RemoveServiceNode(_deploymentIdentifier.GetConsulServiceName(), new ConsulEndPoint { HostName = hostName, Port = port});
        }

        private void SetServiceVersion(string version)
        {
            _consulSimulator.SetServiceVersion(_deploymentIdentifier.GetConsulServiceName(), version);
        }

        private void RemoveService(string serviceName=null)
        {
            _consulSimulator.RemoveService(serviceName ?? _deploymentIdentifier.GetConsulServiceName());
        }

        private void SetConsulIsDown()
        {
            _consulSimulator.SetError(new Exception("fake error"));
        }
    }
}
