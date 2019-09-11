using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.Testing.Shared.Service;
using Metrics;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ConsulNodeSourceFactoryTests : UpdatableConfigTests
    {
        private const string ServiceName = "MyService";
        private const string Env = "prod";
        private  int ConsulPort = DisposablePort.GetPort().Port;
        private const string Zone = "us1a";
        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private DeploymentIdentifier _deploymentIdentifier;

        private ConsulSimulator _consulSimulator;
        private IEnvironment _environment;
        private ConsulNodeSourceFactory _factory;

        public override void OneTimeSetUp()
        {
            _consulSimulator = new ConsulSimulator(ConsulPort);
            base.OneTimeSetUp();
        }

        public override void OneTimeTearDown()
        {
            _consulSimulator.Dispose();
            base.OneTimeTearDown();
        }

        public override void Setup()
        {
            _consulSimulator.Reset();            
            _deploymentIdentifier = new DeploymentIdentifier(ServiceName + "_" + Guid.NewGuid(), Env, _environment);
        }


        public override void TearDown()
        {
            _factory?.Dispose();
        }

        protected override Action<IKernel> AdditionalBindings()
        {
            return k =>
                        {
                            _environment = Substitute.For<IEnvironment>();
                            _environment.ConsulAddress.Returns($"{CurrentApplicationInfo.HostName}:{ConsulPort}");
                            _environment.Zone.Returns(Zone);
                            k.Rebind<IEnvironment>().ToMethod(_ => _environment);
                        };
        }

        [Test]
        public async Task ServiceMissingOnStart()
        {
            await Start();
            (await _factory.IsServiceDeployed(_deploymentIdentifier)).ShouldBeFalse();
            var nodeSource = await _factory.CreateNodeSource(_deploymentIdentifier);
            nodeSource.ShouldBeNull();
        }

        [Test]
        public async Task ServiceBecomesMissing()
        {
            AddService();
            await Start();

            await ShouldCreateNodeSource();

            RemoveService();
            await Task.Delay(800);

            await NodeSourceCannotBeCreated();
        }

        [Test]
        public async Task ServiceAddedWhileRunning()
        {            
            await Start();

            await NodeSourceCannotBeCreated();

            AddService();

            await Task.Delay(800);

            await ShouldCreateNodeSource();
        }

        [Test]
        public async Task ServiceWithNoNodes()
        {
            SetServiceVersion();
            await Start();
            await ShouldCreateNodeSource();
        }

        [Test]
        public async Task ErrorOnStart()
        {
            await Start();
            SetError();
            await Task.Delay(800);
            Should.Throw<EnvironmentException>(async () =>
                                                {
                                                    var nodeSource = await _factory.CreateNodeSource(_deploymentIdentifier);
                                                    nodeSource.GetNodes();
                                                });
            GetHealthStatus().IsHealthy.ShouldBeFalse();
        }

        [Test]
        public async Task ConsulResponsiveAfterError()
        {
            await ChangeConfig<ConsulConfig>(new []{new KeyValuePair<string, string>("Consul.ErrorRetryInterval", TimeSpan.FromMilliseconds(10).ToString())});
            
            SetError();
            await Start();
            _consulSimulator.Reset();
            AddService();
            await Task.Delay(800);
            await ShouldCreateNodeSource();
        }

        [Test]
        public async Task ErrorWhileRunning_ServiceStillAppearsOnList()
        {
            AddService();
            await Start();
            SetError();
            await Task.Delay(800);
            await ShouldCreateNodeSource();
            GetHealthStatus().IsHealthy.ShouldBeFalse();
        }

        [Test]
        public async Task ServiceListShouldAppearOnHealthCheck()
        {
            AddService(deploymentIdentifier: new DeploymentIdentifier("Service1", Env, _environment));
            AddService(deploymentIdentifier: new DeploymentIdentifier("Service2", Env, _environment));
            await Start();
            
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeTrue();

            healthStatus.Message.ShouldContain("Service1");
            healthStatus.Message.ShouldContain("Service2");
        }

        private async Task Start()
        {
            _factory = _unitTestingKernel.Get<ConsulNodeSourceFactory>();
            // try get some NodeSource in order to start init
            try { await _factory.CreateNodeSource(null);} catch { }
        }

        private void SetError()
        {
            _consulSimulator.SetError(new Exception("Fake Error on Consul"));
        }

        private async void AddService(string hostName = Host1, int port = Port1, string version = Version, DeploymentIdentifier deploymentIdentifier = null)
        {
            _consulSimulator.AddServiceNode(deploymentIdentifier?.GetConsulServiceName() ?? _deploymentIdentifier.GetConsulServiceName(), new ConsulEndPoint { HostName = hostName, Port = port, Version = version });
        }

        private void RemoveService()
        {
            _consulSimulator.RemoveService(_deploymentIdentifier.GetConsulServiceName());
        }

        private void SetServiceVersion(string version=Version)
        {
            _consulSimulator.SetServiceVersion(_deploymentIdentifier.GetConsulServiceName(), version);
        }

        private async Task ShouldCreateNodeSource(DeploymentIdentifier expectedDeploymentIdentifier=null)
        {
            (await _factory.IsServiceDeployed(_deploymentIdentifier)).ShouldBeTrue();
            _factory.CreateNodeSource(_deploymentIdentifier??expectedDeploymentIdentifier).Result.ShouldNotBeNull();            
        }

        private async Task NodeSourceCannotBeCreated()
        {
            (await _factory.IsServiceDeployed(_deploymentIdentifier)).ShouldBeFalse();        
            _factory.CreateNodeSource(_deploymentIdentifier).Result.ShouldBeNull();
        }

        private HealthCheckResult GetHealthStatus()
        {
            var healthMonitor = (FakeHealthMonitor)_unitTestingKernel.Get<IHealthMonitor>();
            return healthMonitor.Monitors["ConsulServiceList"].Invoke();
        }

    }
}
