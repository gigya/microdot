using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
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
    public class ConsulNodeMonitorTests
    {
        private const int ConsulPort = 8506;
        private const string DataCenter = "us1";

        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private const string Host2 = "Host2";
        private const int Port2 = 5678;
        private const string Version2 = "2.0.0.1";

        private TestingKernel<ConsoleLog> _testingKernel;
        private INodeMonitor _nodeMonitor;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ConsulSimulator _consulSimulator;
        private DeploymentIdentifier _deploymentIdentifier;
        private ConsulConfig _consulConfig;

        private string _serviceName;

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
        public void Setup()
        {
            _consulSimulator.Reset();
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
                _environmentVariableProvider.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                _environmentVariableProvider.DataCenter.Returns(DataCenter);
                k.Rebind<IEnvironmentVariableProvider>().ToMethod(_ => _environmentVariableProvider);
                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => () => _consulConfig);
            });
            _serviceName = $"MyService_{Guid.NewGuid().ToString().Substring(5)}";

            _deploymentIdentifier = new DeploymentIdentifier(_serviceName, "prod");
            _consulConfig = new ConsulConfig { ErrorRetryInterval = TimeSpan.FromMilliseconds(10) };
        }

        [TearDown]
        public async Task Teardown()
        {
            if (_nodeMonitor!=null)
                await _nodeMonitor.DisposeAsync().ConfigureAwait(false);
            _testingKernel?.Dispose();
        }

        public async Task WaitForUpdates()
        {
            await Task.Delay(1500).ConfigureAwait(false);
        }

        [Test]
        public async Task ServiceExists()
        {
            using (new TraceContext("AddServiceNode"))
                AddServiceNode();

            using (new TraceContext("Init"))
                await Init();

            using (new TraceContext("AssertOneDefaultNode"))
                AssertOneDefaultNode();
        }

        [Test]
        public async Task ServiceAdded()
        {
            using (new TraceContext("init"))
                await Init();

            using (new TraceContext("SetServiceVersion"))
                SetServiceVersion(Version);
            using (new TraceContext("AddServiceNode"))
                AddServiceNode();
            using (new TraceContext("WaitForUpdates"))
                await WaitForUpdates();
            using (new TraceContext("AssertOneDefaultNode"))
                AssertOneDefaultNode();
        }

        [Test]
        public async Task NodeAdded()
        {
            using (new TraceContext("SetServiceVersion"))
                SetServiceVersion(Version);
            using (new TraceContext("AddServiceNode"))
                AddServiceNode();
            using (new TraceContext("Init"))
                await Init();
            
            using (new TraceContext("AssertOneDefaultNode"))
                AssertOneDefaultNode();

            using (new TraceContext("AddServiceNode(Host2)"))
                AddServiceNode(Host2);
            using (new TraceContext("WaitForUpdates"))
                await WaitForUpdates();

            _nodeMonitor.Nodes.Length.ShouldBe(2);
           _nodeMonitor.Nodes[1].Hostname.ShouldBe(Host2);
        }


        [Test]
        public async Task NodeRemoved()
        {
            using (new TraceContext("AddServiceNode"))
                AddServiceNode();
            using (new TraceContext("AddServiceNode(nodeToRemove)"))
                AddServiceNode("nodeToRemove");

            using (new TraceContext("Init"))
                await Init();

            using (new TraceContext("_nodeMonitor.Nodes.Length.ShouldBe(2);"))
                _nodeMonitor.Nodes.Length.ShouldBe(2);

            using (new TraceContext("RemoveServiceEndPoint(nodeToRemove)"))
                RemoveServiceEndPoint("nodeToRemove");
            using (new TraceContext("WaitForUpdates"))
                await WaitForUpdates();

            using (new TraceContext("AssertOneDefaultNode"))
                AssertOneDefaultNode();
        }


        [Test]
        public async Task ServiceVersionHasChanged()
        {
            AddServiceNode();
            AddServiceNode(Host2, Port2, Version2);
            await Init();
            AssertOneDefaultNode();

            SetServiceVersion(Version2);
            await WaitForUpdates();

            await ReloadNodeMonitorIfNeeded();
            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe(Host2);
            _nodeMonitor.Nodes[0].Port.ShouldBe(Port2);
        }

        [Test]
        public async Task StartWithError()
        {
            AddServiceNode();
            SetConsulIsDown();

            await Init();
            
            AssertExceptionIsThrown();                       
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
        public async Task UpgradeVersion()
        {
            AddServiceNode(hostName: "oldVersionHost", version: "1.0.0");
            AddServiceNode(hostName: "newVersionHost", version: "2.0.0");
            SetServiceVersion("1.0.0");

            await Init();

            await ReloadNodeMonitorIfNeeded();
            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe("oldVersionHost");

            SetServiceVersion("2.0.0");
            await WaitForUpdates();

            await ReloadNodeMonitorIfNeeded();
            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe("newVersionHost");
        }

        [Test]
        public async Task ServiceIsDeployedWithNoNodes_ThrowsEnvironmentException()
        {
            SetServiceVersion("1.0.0");
            await Init();
            _nodeMonitor.WasUndeployed.ShouldBeFalse();
            AssertExceptionIsThrown();
        }

        [Test]        
        public async Task ServiceUndeployed_StopMonitoring()
        {
            AddServiceNode();
            await Init();
            _nodeMonitor.WasUndeployed.ShouldBeFalse();

            RemoveService();
            await WaitForUpdates();
            _nodeMonitor.WasUndeployed.ShouldBeTrue();
            var healthRequestsCounterBeforeServiceWasRedeployed = _consulSimulator.HealthRequestsCounter;            

            AddServiceNode();
            await WaitForUpdates();            
            _nodeMonitor.WasUndeployed.ShouldBeTrue("WasUndeployed should still be true because monitoring was already stopped");            
            _consulSimulator.HealthRequestsCounter.ShouldBe(healthRequestsCounterBeforeServiceWasRedeployed, "service monitoring should have been stopped when the service became undeployed");
        }


        private async Task Init()
        {
            using (new TraceContext("createNodeMonitor"))
                _nodeMonitor = _testingKernel.Get<Func<DeploymentIdentifier, INodeMonitor>>()(_deploymentIdentifier);

            using (new TraceContext("nodeMonitor.Init"))
                await _nodeMonitor.Init();            
        }


        private async Task AssertOneDefaultNode()
        {
            await ReloadNodeMonitorIfNeeded();
            _nodeMonitor.WasUndeployed.ShouldBeFalse();
            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe(Host1);
            _nodeMonitor.Nodes[0].Port.ShouldBe(Port1);
        }

        private async Task ReloadNodeMonitorIfNeeded()
        {
            if (_nodeMonitor.WasUndeployed)
            {
                _nodeMonitor.DisposeAsync();
                await Init();
            }
        }

        private void AssertExceptionIsThrown()
        {
            var getNodesAction = (Action) (() =>
            {
                var _ = _nodeMonitor.Nodes;
            });

            getNodesAction.ShouldThrow<EnvironmentException>();
        }

        private async void AddServiceNode(string hostName=Host1, int port=Port1, string version=Version)
        {            
            _consulSimulator.AddServiceNode(_deploymentIdentifier.ToString(), new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceEndPoint(string hostName = Host1, int port = Port1)
        {
            _consulSimulator.RemoveServiceNode(_deploymentIdentifier.ToString(), new ConsulEndPoint { HostName = hostName, Port = port});
        }

        private void SetServiceVersion(string version)
        {
            _consulSimulator.SetServiceVersion(_deploymentIdentifier.ToString(), version);
        }

        private void RemoveService()
        {
            _consulSimulator.RemoveService(_deploymentIdentifier.ToString());
        }

        private void SetConsulIsDown()
        {
            _consulSimulator.SetError(new Exception("fake error"));
        }


    }
}
