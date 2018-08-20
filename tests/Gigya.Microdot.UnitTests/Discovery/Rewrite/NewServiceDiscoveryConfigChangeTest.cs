using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NUnit.Framework;
using Shouldly;
using IConsulClient = Gigya.Microdot.ServiceDiscovery.IConsulClient;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class NewServiceDiscoveryConfigChangeTest
    {
        private const string ServiceName = "ServiceName";
        private NewServiceDiscovery _serviceDiscovery;
        private Dictionary<string, string> _configDic;        
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private ConsulClientMock _consulClientMock;
        private DiscoveryConfig _discoveryConfig;
        public const int Repeat = 1;
        private const string ServiceVersion = "1.0.0.0";

        [SetUp]
        public async Task Setup()
        {
            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironment>().To<EnvironmentInstance>();
                k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>();
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(_ => () => _discoveryConfig);
                _consulClientMock = new ConsulClientMock();
                _consulClientMock.SetResult(new EndPointsResult { EndPoints = new[] { new ConsulEndPoint { HostName = "dumy", Version = ServiceVersion } }, ActiveVersion = ServiceVersion, IsQueryDefined = true });
                k.Rebind<Func<string,IConsulClient>>().ToMethod(c=>s=>_consulClientMock);
            }, _configDic);

            _discoveryConfig = new DiscoveryConfig { Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            _serviceDiscovery = _unitTestingKernel.Get<Func<string, ReachabilityChecker, NewServiceDiscovery>>()("ServiceName", x => Task.FromResult(true));
           }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
            _consulClientMock.Dispose();
        }

        [Repeat(Repeat)]

        public async Task DiscoveySettingAreUpdateOnConfigChange()
        {
            _discoveryConfig.Services[ServiceName].Source = "Config";
            _discoveryConfig.Services[ServiceName].Hosts = "host3";

            var node = await _serviceDiscovery.GetNode();
            Assert.AreEqual("Config", _serviceDiscovery.LastServiceConfig.Source);
            Assert.AreEqual("host3", node.Hostname);
        }

        [Repeat(Repeat)]

        public async Task ServiceSourceIsLocal()
        {
            _discoveryConfig.Services[ServiceName].Source = "Local";
            var node = await _serviceDiscovery.GetNode();
            node.Hostname.ShouldContain(CurrentApplicationInfo.HostName);
        }

    }
}