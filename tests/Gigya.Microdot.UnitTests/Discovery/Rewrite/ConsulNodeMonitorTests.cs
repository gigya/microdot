using System;
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
        private string _serviceDeployment;
        private ConsulConfig _consulConfig;

        private string _serviceName;

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _consulSimulator = new ConsulSimulator(ConsulPort);

        }

        [OneTimeTearDown]
        public void TearDownConsulListener()
        {
            _testingKernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
                _environmentVariableProvider.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                _environmentVariableProvider.DataCenter.Returns(DataCenter);
                k.Rebind<IEnvironmentVariableProvider>().ToMethod(_ => _environmentVariableProvider);

                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => () => _consulConfig);
            });

            _serviceName = $"MyService_{Guid.NewGuid().ToString().Substring(5)}";            
            _consulSimulator.Reset();

            _serviceDeployment = $"{_serviceName}-prod";
            _consulConfig = new ConsulConfig {ErrorRetryInterval = TimeSpan.FromMilliseconds(10)};
        }

        [TearDown]
        public void Teardown()
        {
            _nodeMonitor?.Dispose();            
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _consulSimulator.Dispose();
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
            SetServiceVersion(Version);
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
            _nodeMonitor.Nodes.Length.ShouldBe(2);
            _nodeMonitor.Nodes[1].Hostname.ShouldBe(Host2);
        }


        [Test]
        public async Task NodeRemoved()
        {
            AddServiceNode();
            AddServiceNode("nodeToRemove");

            await Init();

            _nodeMonitor.Nodes.Length.ShouldBe(2);

            RemoveServiceEndPoint("nodeToRemove");
            await WaitForUpdates();

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

            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe("oldVersionHost");

            SetServiceVersion("2.0.0");
            await WaitForUpdates();

            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe("newVersionHost");
        }

        [Test]
        public async Task ServiceIsDeployedWithNoNodes_ThrowsEnvironmentException()
        {
            SetServiceVersion("1.0.0");
            await Init();
            _nodeMonitor.IsDeployed.ShouldBeTrue();
            AssertExceptionIsThrown();
        }

        private Task Init()
        {
            _nodeMonitor = _testingKernel.Get<Func<string, INodeMonitor>>()(_serviceDeployment);
            return _nodeMonitor.Init();
        }


        private void AssertOneDefaultNode()
        {
            _nodeMonitor.IsDeployed.ShouldBeTrue();
            _nodeMonitor.Nodes.Length.ShouldBe(1);
            _nodeMonitor.Nodes[0].Hostname.ShouldBe(Host1);
            _nodeMonitor.Nodes[0].Port.ShouldBe(Port1);
        }

        private void AssertExceptionIsThrown()
        {
            var getNodesAction = (Action) (() =>
            {
                var _ = _nodeMonitor.Nodes;
            });

            getNodesAction.ShouldThrow<EnvironmentException>();
        }

        private async void AddServiceNode(string hostName=Host1, int port=Port1, string version=Version, string serviceName=null)
        {            
            _consulSimulator.AddServiceNode(serviceName??_serviceDeployment, new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceEndPoint(string hostName = Host1, int port = Port1, string serviceName=null)
        {
            _consulSimulator.RemoveServiceNode(serviceName??_serviceDeployment, new ConsulEndPoint { HostName = hostName, Port = port});
        }

        private void SetServiceVersion(string version, string serviceName=null)
        {
            _consulSimulator.SetServiceVersion(serviceName??_serviceDeployment, version);
        }

        private void SetConsulIsDown()
        {
            _consulSimulator.SetError(new Exception("fake error"));
        }


    }
}
