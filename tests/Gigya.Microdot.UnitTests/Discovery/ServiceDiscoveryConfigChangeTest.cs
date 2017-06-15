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

            await WaitForConfigChange(() => {});
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
        }

        [TestCase("Services.ServiceName")]
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
        public async Task ServiceSourceIsLocal(string serviceName)
        {
            await WaitForConfigChange(() => _configDic[$"Discovery.{serviceName}.Source"] = "Local");
            var remotHostPull = _serviceDiscovery.GetNextHost();
            remotHostPull.Result.HostName.ShouldContain(CurrentApplicationInfo.HostName);
        }


        private async Task WaitForConfigChange(Action update)
        {
            var oldValue = _serviceDiscovery.LastConfig;
            update();
            _configRefresh.RaiseChangeEvent();

            for (int i = 0; i < 500; i++)
            {
                if (_serviceDiscovery.LastConfig != null && Equals(oldValue, _serviceDiscovery.LastConfig) == false)
                    return;

                await Task.Delay(10);
            }

            throw new TimeoutException("time out");
        }
    }
}