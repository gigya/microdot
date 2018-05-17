using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
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
    public class ConsulNodeSourceTests
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
        private INodeSource _nodeSource;
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
        public async Task Setup()
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
            _consulConfig = new ConsulConfig {ErrorRetryInterval = TimeSpan.FromMilliseconds(10)};
        }

        [TearDown]
        public async Task Teardown()
        {
            _nodeSource?.Shutdown();
            _testingKernel?.Dispose();
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

            AssertOneDefaultNode();
        }

        [Test]
        public async Task ServiceNotExists()
        {
            await Init();
            _nodeSource.ShouldBeNull();
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
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe(Host2);
            nodes[0].Port.ShouldBe(Port2);
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
        public async Task ErrorOnStart()
        {
            SetConsulIsDown();
            await Init();

            AssertExceptionIsThrown();            
        }

        [Test]
        public async Task UpgradeVersion()
        {
            AddServiceNode(hostName: "oldVersionHost", version: "1.0.0");
            AddServiceNode(hostName: "newVersionHost", version: "2.0.0");
            SetServiceVersion("1.0.0");

            await Init();

            await ReloadNodeMonitorIfNeeded();
            var nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe("oldVersionHost");

            SetServiceVersion("2.0.0");
            await WaitForUpdates();

            await ReloadNodeMonitorIfNeeded();
            nodes = _nodeSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe("newVersionHost");
        }

        [Test]
        public async Task ServiceIsDeployedWithNoNodes_ThrowsEnvironmentException()
        {
            SetServiceVersion("1.0.0");
            await Init();
            _nodeSource.WasUndeployed.ShouldBeFalse();
            AssertExceptionIsThrown();
        }

        [Test]        
        public async Task ServiceUndeployed_StopMonitoring()
        {
            AddServiceNode();
            await Init();
            _nodeSource.WasUndeployed.ShouldBeFalse();

            RemoveService();
            await WaitForUpdates();
            _nodeSource.WasUndeployed.ShouldBeTrue();

            AddServiceNode();
            await WaitForUpdates();            
            _nodeSource.WasUndeployed.ShouldBeTrue("WasUndeployed should still be true because monitoring was already stopped");            
        }

        [Test]
        public async Task ServiceDeployedInLowerCase()
        {
            AddServiceNode(serviceName: _deploymentIdentifier.ToString().ToLower());
            await Init();

            AssertOneDefaultNode();
        }


        [Test]
        public async Task ServiceCasingIsChanged_MarkAsUndeployedInOrderToCauseItToBeReloaded()
        {
            var lowerCaseServiceName = _deploymentIdentifier.ToString().ToLower();
            AddServiceNode(serviceName: lowerCaseServiceName);
            await Init();

            AddServiceNode();
            RemoveService(serviceName: lowerCaseServiceName);
            await WaitForUpdates();

            _nodeSource.WasUndeployed.ShouldBeTrue();            
        }



        private async Task Init()
        {
            var factory = _testingKernel.Get<ConsulNodeSourceFactory>();
            _nodeSource = await factory.TryCreateNodeSource(_deploymentIdentifier);            
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

        private void AssertExceptionIsThrown()
        {
            var getNodesAction = (Action) (() =>
            {
                _nodeSource.GetNodes();
            });

            getNodesAction.ShouldThrow<EnvironmentException>();
        }

        private async void AddServiceNode(string hostName=Host1, int port=Port1, string version=Version, string serviceName=null)
        {            
            _consulSimulator.AddServiceNode(serviceName ?? _deploymentIdentifier.ToString(), new ConsulEndPoint {HostName = hostName, Port = port, Version = version});         
        }

        private async void RemoveServiceEndPoint(string hostName = Host1, int port = Port1)
        {
            _consulSimulator.RemoveServiceNode(_deploymentIdentifier.ToString(), new ConsulEndPoint { HostName = hostName, Port = port});
        }

        private void SetServiceVersion(string version)
        {
            _consulSimulator.SetServiceVersion(_deploymentIdentifier.ToString(), version);
        }

        private void RemoveService(string serviceName=null)
        {
            _consulSimulator.RemoveService(serviceName ?? _deploymentIdentifier.ToString());
        }

        private void SetConsulIsDown()
        {
            _consulSimulator.SetError(new Exception("fake error"));
        }


    }
}
