using System;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.Testing.Shared;
using Metrics;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class ConsulServiceListMonitorTests
    {
        private const string ServiceName = "MyService-prod";
        private const int ConsulPort = 8502;
        private const string DataCenter = "us1";
        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private string _serviceName;

        private TestingKernel<ConsoleLog> _testingKernel;
        private ConsulConfig _consulConfig;
        private IConsulServiceListMonitor _consulServiceListMonitor;
        private ConsulSimulator _consulSimulator;
        private IEnvironmentVariableProvider _environmentVariableProvider;

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

                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => () => _consulConfig);

                k.Rebind<IConsulServiceListMonitor>().To<ConsulServiceListMonitor>().InTransientScope(); // Set in transient scope in order to get a new instance per test.
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
            _consulSimulator.Reset();
            _serviceName = ServiceName + "_" + Guid.NewGuid();

            _consulConfig = new ConsulConfig();
        }


        [TearDown]
        public void TearDown()
        {
            _consulServiceListMonitor?.Dispose();
        }


        [Test]
        public async Task ServiceMissingOnStart()
        {
            await Init();
            _consulServiceListMonitor.Services.ShouldNotContain(_serviceName);
        }

        [Test]
        public async Task ServiceBecomesMissing()
        {
            AddService();
            await Init();

            ServiceShouldExistOnList();

            RemoveService();
            await Task.Delay(300);

            ServiceShouldNotExistOnList();
        }

        [Test]
        public async Task ServiceAddedWhileRunning()
        {            
            await Init();

            ServiceShouldNotExistOnList();

            AddService();

            await Task.Delay(300);

            ServiceShouldExistOnList();
        }

        [Test]
        public async Task ErrorOnStart()
        {
            await Init();
            SetError();
            await Task.Delay(300);
            Should.Throw<EnvironmentException>(() =>
                                                {
                                                    var _ = _consulServiceListMonitor.Services;
                                                });
            GetHealthStatus().IsHealthy.ShouldBeFalse();
        }

        [Test]
        public async Task ConsulResponsiveAfterError()
        {
            _consulConfig.ErrorRetryInterval = TimeSpan.FromMilliseconds(10);
            SetError();
            await Init();
            _consulSimulator.Reset();
            AddService();
            await Task.Delay(300);
            ServiceShouldExistOnList();
        }

        [Test]
        public async Task ErrorWhileRunning_ServiceStillAppearsOnList()
        {
            AddService();
            await Init();
            SetError();
            await Task.Delay(300);
            ServiceShouldExistOnList();
            GetHealthStatus().IsHealthy.ShouldBeFalse();
        }

        [Test]
        public async Task ServiceListShouldAppearOnHealthCheck()
        {
            AddService(serviceName: "Service1");
            AddService(serviceName: "Service2");
            await Init();
            
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeTrue();

            healthStatus.Message.ShouldContain("Service1");
            healthStatus.Message.ShouldContain("Service2");
        }

        [Test]
        public async Task DuplicateService_HealthCheckIsRed()
        {
            AddService(serviceName: _serviceName);
            AddService(serviceName: _serviceName.ToLower());
            AddService("OtherService");
            await Init();
            
            var healthStatus = GetHealthStatus();
            healthStatus.IsHealthy.ShouldBeFalse();
            healthStatus.Message.ShouldContain(_serviceName);
            healthStatus.Message.ShouldContain(_serviceName.ToLower());
            healthStatus.Message.ShouldNotContain("OtherService", "When ther exists duplicate services, only duplicate services should be listed on the health check message");
        }

        public Task Init()
        {
            _consulServiceListMonitor = _testingKernel.Get<IConsulServiceListMonitor>();
            return _consulServiceListMonitor.Init();
        }

        private void SetError()
        {
            _consulSimulator.SetError(new Exception("Fake Error on Consul"));
        }

        private async void AddService(string hostName = Host1, int port = Port1, string version = Version, string serviceName = null)
        {
            _consulSimulator.AddServiceNode(serviceName ?? _serviceName, new ConsulEndPoint { HostName = hostName, Port = port, Version = version });
        }

        private void RemoveService()
        {
            _consulSimulator.RemoveService(_serviceName);
        }

        private void ServiceShouldExistOnList()
        {
            _consulServiceListMonitor.Services.ShouldContain(_serviceName);
        }

        private void ServiceShouldNotExistOnList()
        {
            _consulServiceListMonitor.Services.ShouldNotContain(_serviceName);
        }

        private HealthCheckResult GetHealthStatus()
        {
            var healthMonitor = (FakeHealthMonitor)_testingKernel.Get<IHealthMonitor>();
            return healthMonitor.Monitors["ConsulServiceList"].Invoke();
        }

    }
}
