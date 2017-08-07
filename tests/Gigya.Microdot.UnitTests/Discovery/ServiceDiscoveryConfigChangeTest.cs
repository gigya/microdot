using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Utils;
using Ninject;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture]
    public class ServiceDiscoveryConfigChangeTest
    {
        private ServiceDiscovery.ServiceDiscovery _serviceDiscovery;
        private Dictionary<string, string> _configDic;
        private ManualConfigurationEvents _configRefresh;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private IConsulClient _consulAdapterMock;
        public const int Repeat = 1;

        [SetUp]
        public async Task Setup()
        {
            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                k.Rebind<IEnvironmentVariableProvider>().To<EnvironmentVariableProvider>();
                _consulAdapterMock = Substitute.For<IConsulClient>();
                _consulAdapterMock.GetEndPoints(Arg.Any<string>()).Returns(Task.FromResult(new EndPointsResult { EndPoints = new[] { new ConsulEndPoint { HostName = "dumy" } } }));
                k.Rebind<IConsulClient>().ToConstant(_consulAdapterMock);
            }, _configDic);

            _configRefresh = _unitTestingKernel.Get<ManualConfigurationEvents>();
            _serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityChecker, ServiceDiscovery.ServiceDiscovery>>()("ServiceName", x => Task.FromResult(true));
           }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
        }

        [TestCase("Services.ServiceName")]
        [Repeat(Repeat)]

        public async Task DiscoveySettingAreUpdateOnConfigChange(string serviceName)
        {
            await WaitForConfigChange(() =>
                  {
                      _configDic[$"Discovery.{serviceName}.Source"] = "Config";
                      _configDic[$"Discovery.{serviceName}.Hosts"] = "localhost";
                  });

            Assert.AreEqual(DiscoverySource.Config, _serviceDiscovery.LastConfig.Source);
        }

        [TestCase("Services.OtherServiceName")]
        [Repeat(Repeat)]

        public void DiscoverySettingUpdatingUnrelatedConfig_ShouldNotChange(string serviceName)
        {

            Func<Task> act = () => WaitForConfigChange(() =>
            {
                _configDic[$"Discovery.{serviceName}.Source"] = "Config";
                _configDic[$"Discovery.{serviceName}.Hosts"] = "localhost";
            });

            act.ShouldThrowAsync<TimeoutException>();
        }


        [TestCase("Services.ServiceName")]
        [Repeat(Repeat)]

        public async Task HostShouldUpdateFromConfig(string serviceName)
        {

            await WaitForConfigChange(() =>
            {
                _configDic[$"Discovery.{serviceName}.Source"] = "Config";
                _configDic[$"Discovery.{serviceName}.Hosts"] = "host3";
            });
            var nextHost = _serviceDiscovery.GetNextHost();
            Assert.AreEqual("host3", nextHost.Result.HostName);
        }

        [TestCase("Services.ServiceName")]
        [Repeat(Repeat)]

        public async Task ServiceSourceIsLocal(string serviceName)
        {
            await WaitForConfigChange(() =>
            _configDic[$"Discovery.{serviceName}.Source"] = "Local"
            );
            var remoteHostPull = _serviceDiscovery.GetNextHost();
            remoteHostPull.Result.HostName.ShouldContain(CurrentApplicationInfo.HostName);
        }



        private async Task WaitForConfigChange(Action update)
        {
            var waitForInit = await _serviceDiscovery.GetNextHost();
            var task = _serviceDiscovery.EndPointsChanged.WhenEventReceived();
            update();
            _configRefresh.RaiseChangeEvent();

            await task;
        }
    }
}