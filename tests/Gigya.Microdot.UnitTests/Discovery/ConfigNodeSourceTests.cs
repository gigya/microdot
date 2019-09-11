using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;


namespace Gigya.Microdot.UnitTests.Discovery
{
    public class ConfigNodeSourceTests
    {
        private const string ServiceName = "MyService";

        private TestingKernel<ConsoleLog> _kernel;
        private INodeSource _configNodeSource;
        private DiscoveryConfig _discoveryConfig;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _kernel = new TestingKernel<ConsoleLog>();
            _kernel.Rebind<Func<DiscoveryConfig>>().ToMethod(c => () => _discoveryConfig);            
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _kernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            var environment = Substitute.For<IEnvironment>();
            var deployment = new DeploymentIdentifier(ServiceName, "prod", environment);

            _configNodeSource = _kernel.Get<Func<DeploymentIdentifier, ConfigNodeSource>>()(deployment);            
        }

        private async Task SetConfigHosts(string hosts)
        {
            _discoveryConfig = new DiscoveryConfig {Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig()) };
            if (hosts != null)
                _discoveryConfig.Services[ServiceName].Hosts = hosts;
        }

        [Test]
        public void ThrowExceptionIfConfigIsEmpty()
        {
            SetConfigHosts(null);
            var getNodes = (Action)(() => GetNodes());
            getNodes.ShouldThrow<EnvironmentException>();
        }

        [Test]
        public void ConfigWithOneHost()
        {
            SetConfigHosts("myHost");
            var nodes = GetNodes();
            nodes[0].Hostname.ShouldBe("myHost");
            nodes[0].Port.ShouldBe(null);
        }

        [Test]
        public void ConfigWithOneHostAndPort()
        {
            SetConfigHosts("myHost:123");
            var nodes = GetNodes();
            nodes[0].Hostname.ShouldBe("myHost");
            nodes[0].Port.ShouldBe(123);
        }

        [Test]
        public void ConfigWithMoreThanOneHost()
        {
            SetConfigHosts("host1,host2:333");
            var nodes = GetNodes();
            nodes[0].Hostname.ShouldBe("host1");
            nodes[0].Port.ShouldBe(null);
            nodes[1].Hostname.ShouldBe("host2");
            nodes[1].Port.ShouldBe(333);
        }

        [Test]
        public void ConfigWithEmptySpaces()
        {
            SetConfigHosts("host1, host2,,,host3");
            var nodes = GetNodes();
            nodes[0].Hostname.ShouldBe("host1");
            nodes[1].Hostname.ShouldBe("host2");
            nodes[2].Hostname.ShouldBe("host3");
        }

        [Test]
        public void ConfigWithIncorrectPortDefinition()
        {
            SetConfigHosts("myHost:1:2:3");
            ShouldThrowExtensions.ShouldThrow(()=>GetNodes(), typeof(ConfigurationException));
        }

        [Test]
        public void UpdateNodesWhenConfigurationUpdated()
        {
            SetConfigHosts("host1");
            var nodes = GetNodes();
            nodes[0].Hostname.ShouldBe("host1");

            SetConfigHosts("host2");
            nodes = GetNodes();
            nodes[0].Hostname.ShouldBe("host2");

        }

        private Node[] GetNodes()
        {
            return _configNodeSource.GetNodes();
        }
    }
}

