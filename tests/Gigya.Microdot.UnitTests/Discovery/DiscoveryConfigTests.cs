using System;
using System.Collections.Generic;
using System.Globalization;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Shared;
using Ninject;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture]
    public class DiscoveryConfigTests
    {
        private const string SERVICE_NAME = "ServiceName";

        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private Func<string, ServiceDiscoveryConfig> _settingsFactory;

        [SetUp]
        public void Setup()
        {
            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(_ => { }, _configDic);

            _settingsFactory = name => _unitTestingKernel.Get<Func<DiscoveryConfig>>()().Services[name];
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
        }

        [Test]
        public void DefaultDiscoverySourceIsConsul()
        {
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Source.ShouldBe(ConsulDiscoverySource.Name);
        }

        [Test]
        public void DiscoverySourceIsConfig()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Hosts"] = "localhost";
            _configDic["Discovery.Source"] = "Config";
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Source.ShouldBe("Config");
        }

        [Test]
        public void ServiceSourceIsConfig()
        {
            SetServiceSourceConfig();
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Source.ShouldBe("Config");
        }


        [Test]
        public void FirstAttemptDelaySecondsDefaultValue()
        {
            const double expectedDefault = 0.001;
            _configDic.Clear();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.FirstAttemptDelaySeconds.ShouldBe(expectedDefault);
        }

        [Test]
        public void FirstAttemptDelaySeconds()
        {
            const double expectedValue = 0.002;
            ChangeConfig("Discovery.FirstAttemptDelaySeconds", expectedValue.ToString(CultureInfo.InvariantCulture));
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.FirstAttemptDelaySeconds.ShouldBe(expectedValue);
        }

        [Test]
        public void ServiceFirstAttemptDelaySeconds()
        {
            const double expectedValue = 0.003;
            ChangeConfig($"Discovery.Services.{SERVICE_NAME}.FirstAttemptDelaySeconds", expectedValue.ToString(CultureInfo.InvariantCulture));
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.FirstAttemptDelaySeconds.ShouldBe(expectedValue);
        }

        [Test]
        public void MaxAttemptDelaySecondsDefaultValue()
        {
            const double expectedDefault = 10;
            var settings = _settingsFactory(SERVICE_NAME);

            settings.MaxAttemptDelaySeconds.ShouldBe(expectedDefault);
        }

        [Test]
        public void MaxAttemptDelaySeconds()
        {
            const double expectedValue = 20;
            ChangeConfig("Discovery.MaxAttemptDelaySeconds", expectedValue.ToString(CultureInfo.InvariantCulture));
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);

            settings.MaxAttemptDelaySeconds.ShouldBe(expectedValue);
        }

        [Test]
        public void ServiceMaxAttemptDelaySeconds()
        {
            const double expectedValue = 30;
            ChangeConfig($"Discovery.Services.{SERVICE_NAME}.MaxAttemptDelaySeconds", expectedValue.ToString(CultureInfo.InvariantCulture));
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.MaxAttemptDelaySeconds.ShouldBe(expectedValue);
        }

        [Test]
        public void DelayMultiplierDefaultValue()
        {
            const double expectedDefault = 2;
            var settings = _settingsFactory(SERVICE_NAME);
            settings.DelayMultiplier.ShouldBe(expectedDefault);
        }

        [Test]
        public void DelayMultiplier()
        {
            const double expectedValue = 3;
            ChangeConfig("Discovery.DelayMultiplier", expectedValue.ToString(CultureInfo.InvariantCulture));
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.DelayMultiplier.ShouldBe(expectedValue);
        }

        [Test]
        public void ServiceDelayMultiplier()
        {
            const double expectedValue = 30;
            _configDic[$"Discovery.Services.{SERVICE_NAME}.DelayMultiplier"] = expectedValue.ToString(CultureInfo.InvariantCulture);
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.DelayMultiplier.ShouldBe(expectedValue);
        }

        [Test]
        public void DefaultRequestTimeout()
        {
            var settings = _settingsFactory(SERVICE_NAME);
            settings.RequestTimeout.ShouldBeNull();
        }

        [Test]
        public void RequestTimeout()
        {
            string expectedValue = "00:00:15";
            ChangeConfig("Discovery.RequestTimeout", expectedValue);
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.RequestTimeout.ShouldBe(TimeSpan.Parse(expectedValue, CultureInfo.InvariantCulture));
        }

        [Test]
        public void ServiceRequestTimeout()
        {
            string expectedValue = "00:00:10";
            _configDic["Discovery.RequestTimeout"] = "00:00:15";
            _configDic[$"Discovery.Services.{SERVICE_NAME}.RequestTimeout"] = expectedValue;
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.RequestTimeout.ShouldBe(TimeSpan.Parse(expectedValue, CultureInfo.InvariantCulture));
        }

        [Test]
        public void DefaultScope()
        {
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Scope.ShouldBe(ServiceScope.Environment);
        }

        [Test]
        public void Scope()
        {
            var expectedValue = ServiceScope.DataCenter;
            ChangeConfig("Discovery.Scope", expectedValue.ToString());
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Scope.ShouldBe(expectedValue);
        }

        [Test]
        public void ScopeOfService()
        {
            var expectedValue = ServiceScope.DataCenter;
            ChangeConfig($"Discovery.Services.{SERVICE_NAME}.Scope", expectedValue.ToString());
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Scope.ShouldBe(expectedValue);
        }

        [Test]
        public void DefaultPort()
        {
            const int expectedValue = 89940;
            _configDic[$"Discovery.Services.{SERVICE_NAME}.DefaultPort"] = expectedValue.ToString();
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            settings.DefaultPort.ShouldBe(expectedValue);
        }

        [Test]
        public void ServiceSourceChanged()
        {
            SetServiceSourceConfig();
            RunSystemInit();
            var settings = _settingsFactory(SERVICE_NAME);
            Assert.AreEqual("Config", settings.Source);
        }

        [Test]
        public void CaseInsensitiveOnServiceName()
        {
            SetServiceSourceLocal();
            var lowerCaseServiceName = SERVICE_NAME.ToLower();

            var settings2 = _settingsFactory(SERVICE_NAME);
            var settings = _settingsFactory(lowerCaseServiceName);

            //if service don't found it will return defult Consul
            Assert.AreEqual(settings2.Source, settings.Source);
        }

        [Test]
        public void ServiceNotExistRetunDefultSource()
        {
            var settings = _settingsFactory("not exists");
            Assert.AreEqual(ConsulDiscoverySource.Name, settings.Source);
        }

        private void SetServiceSourceLocal()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Source"] = "Local";
        }

        private void SetServiceSourceConfig()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Source"] = "Config";
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Hosts"] = "localhost";
        }

        private void ChangeConfig(string configKey, string newValue)
        {
            _configDic[configKey] = newValue;
        }

        private void RunSystemInit()
        {
            _unitTestingKernel.Get<SystemInitializerBase>().Init();
        }
    }
}