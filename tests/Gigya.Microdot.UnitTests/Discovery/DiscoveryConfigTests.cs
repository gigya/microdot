using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Ninject;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    public class DiscoveryConfigTests : UpdatableConfigTests
    {
        private const string SERVICE_NAME = "ServiceName";
        protected Func<string, ServiceDiscoveryConfig> _settingsFactory;

        public override void Setup()
        {
            base.Setup();

            _settingsFactory = name => _unitTestingKernel.Get<Func<DiscoveryConfig>>()().Services[name];
        }

        public override void OneTimeSetUp() { }

        protected override Action<IKernel> AdditionalBindings()
        {
            return null;
        }

        [Test]
        public void DefaultDiscoverySourceIsConsul()
        {
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Source.ShouldBe(ConsulDiscoverySource.Name);
        }

        [Test]
        public async Task DiscoverySourceIsConfig()
        {
            await ChangeConfig<DiscoveryConfig>(new[]
                                    {
                                        new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.Hosts", "localhost"),
                                        new KeyValuePair<string, string>("Discovery.Source", "Config")
                                    });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Source.ShouldBe("Config");
        }

        [Test]
        public async Task ServiceSourceIsConfig()
        {
            await SetServiceSourceConfig();
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
        public async Task FirstAttemptDelaySeconds()
        {
            const double expectedValue = 0.002;
            await ChangeConfig<DiscoveryConfig>(new []
                                {
                                    new KeyValuePair<string, string>("Discovery.FirstAttemptDelaySeconds",
                                        expectedValue.ToString(CultureInfo.InvariantCulture))
                                });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.FirstAttemptDelaySeconds.ShouldBe(expectedValue);
        }

        [Test]
        public async Task ServiceFirstAttemptDelaySeconds()
        {
            const double expectedValue = 0.003;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.FirstAttemptDelaySeconds",
                                    expectedValue.ToString(CultureInfo.InvariantCulture))
                            });
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
        public async Task MaxAttemptDelaySeconds()
        {
            const double expectedValue = 20;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>("Discovery.MaxAttemptDelaySeconds",
                                    expectedValue.ToString(CultureInfo.InvariantCulture))
                            });
            var settings = _settingsFactory(SERVICE_NAME);

            settings.MaxAttemptDelaySeconds.ShouldBe(expectedValue);
        }

        [Test]
        public async Task ServiceMaxAttemptDelaySeconds()
        {
            const double expectedValue = 30;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.MaxAttemptDelaySeconds",
                                    expectedValue.ToString(CultureInfo.InvariantCulture))
                            });
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
        public async Task DelayMultiplier()
        {
            const double expectedValue = 3;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>("Discovery.DelayMultiplier",
                                    expectedValue.ToString(CultureInfo.InvariantCulture))
                            });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.DelayMultiplier.ShouldBe(expectedValue);
        }

        [Test]
        public async Task ServiceDelayMultiplier()
        {
            const double expectedValue = 30;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.DelayMultiplier",
                                    expectedValue.ToString(CultureInfo.InvariantCulture))
                            });
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
        public async Task RequestTimeout()
        {
            string expectedValue = "00:00:15";
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>("Discovery.RequestTimeout",
                                    expectedValue.ToString(CultureInfo.InvariantCulture))
                            });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.RequestTimeout.ShouldBe(TimeSpan.Parse(expectedValue, CultureInfo.InvariantCulture));
        }

        [Test]
        public async Task ServiceRequestTimeout()
        {
            string expectedValue = "00:00:10";
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>("Discovery.RequestTimeout", "00:00:15"),
                                new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.RequestTimeout", expectedValue)
                            });
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
        public async Task Scope()
        {
            var expectedValue = ServiceScope.DataCenter;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>("Discovery.Scope", expectedValue.ToString())
                            });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Scope.ShouldBe(expectedValue);
        }

        [Test]
        public async Task ScopeOfService()
        {
            var expectedValue = ServiceScope.DataCenter;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.Scope", expectedValue.ToString())
                            });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.Scope.ShouldBe(expectedValue);
        }

        [Test]
        public async Task DefaultPort()
        {
            const int expectedValue = 89940;
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.DefaultPort", expectedValue.ToString())
                            });
            var settings = _settingsFactory(SERVICE_NAME);
            settings.DefaultPort.ShouldBe(expectedValue);
        }

        [Test]
        public async Task ServiceSourceChanged()
        {
            await SetServiceSourceConfig();
            var settings = _settingsFactory(SERVICE_NAME);
            Assert.AreEqual("Config", settings.Source);
        }

        [Test]
        public async Task CaseInsensitiveOnServiceName()
        {
            await ChangeConfig<DiscoveryConfig>(new[]
                                    {
                                        new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.Source", "Local")
                                    });
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
        
        private async Task SetServiceSourceConfig()
        {
            await ChangeConfig<DiscoveryConfig>(new []
                                    {
                                        new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.Source", "Config"),
                                        new KeyValuePair<string, string>($"Discovery.Services.{SERVICE_NAME}.Hosts", "localhost"),
                                    });
        }
    }
}