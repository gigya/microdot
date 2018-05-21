using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class DiscoveryFactoryTests
    {
        private const string Consul = "Consul";        
        private const string Config = "Config";
        private const string Local = "Local";

        private const string ServiceName = "ServiceName";
        private const string Env = "env";

        private IDiscoveryFactory _factory;

        private TestingKernel<ConsoleLog> _kernel;
        private INodeSource _consulSource;
        private INodeSource _configSource;
        private bool _consulSourceWasUndeployed;
        private DiscoveryConfig _discoveryConfig;
        private INodeSourceFactory _consulNodeSourceFactory;


        [OneTimeSetUp]
        public void SetupKernel()
        {
            _kernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<INodeSourceFactory>().ToMethod(c => _consulNodeSourceFactory);                
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(c=> () => _discoveryConfig);
                k.Rebind<IDiscoveryFactory>().To<DiscoveryFactory>().InSingletonScope();
            });
        }

        [SetUp]
        public void Setup()
        {
            _consulSourceWasUndeployed = false;
            _consulSource = Substitute.For<INodeSource>();
            _configSource = Substitute.For<INodeSource>();
            _consulSource.WasUndeployed.Returns(_ => _consulSourceWasUndeployed);

            _consulNodeSourceFactory = Substitute.For<INodeSourceFactory>();
            _consulNodeSourceFactory.Type.Returns(Consul);
            _consulNodeSourceFactory.TryCreateNodeSource(Arg.Any<DeploymentIdentifier>()).Returns(_=> _consulSourceWasUndeployed? null : _consulSource);            

            _discoveryConfig = new DiscoveryConfig();
            _discoveryConfig.Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig());

            _factory = _kernel.Get<IDiscoveryFactory>();
        }

        [Test]
        public async Task TryCreateNodeSource_ReturnConsulSource()
        {
            ConfigureServiceSource(Consul);
            var source = await TryCreateNodeSource();
            source.ShouldBe(_consulSource);
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnConsulLoadBalancer()
        {
            ConfigureServiceSource(Consul);
            var loadBalancer = await TryCreateLoadBalancer();
            loadBalancer.NodeSource.ShouldBe(_consulSource);            
        }

        [Test]
        public async Task TryCreateNodeSource_ReturnConfigSource()
        {
            ConfigureServiceSource(Config);
            var source = await TryCreateNodeSource(env: null);
            source.GetType().ShouldBe(typeof(ConfigNodeSource));
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnConfigLoadBalancer()
        {
            ConfigureServiceSource(Config);
            var loadBalancer = await TryCreateLoadBalancer(env: null);            
            loadBalancer.NodeSource.GetType().ShouldBe(typeof(ConfigNodeSource));
        }

        [Test]
        public async Task TryCreateNodeSource_ReturnLocalSource()
        {
            ConfigureServiceSource(Local);
            var source = await TryCreateNodeSource(env: null);
            source.GetType().ShouldBe(typeof(LocalNodeSource));
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnLocalLoadBalancer()
        {
            ConfigureServiceSource(Local);
            var loadBalancer = await TryCreateLoadBalancer(env: null);            
            loadBalancer.NodeSource.GetType().ShouldBe(typeof(LocalNodeSource));
        }

        [Test]
        public async Task TryCreateNodeSource_NotSupportsMultipleEnvironments_EnvironmentSpecific_ReturnNull()
        {
            ConfigureServiceSource(Config);
            var source = await TryCreateNodeSource();
            source.ShouldBeNull();            
        }

        [Test]
        public async Task TryCreateLoadBalancer__NotSupportsMultipleEnvironments_EnvironmentSpecific_ReturnNull()
        {
            ConfigureServiceSource(Config);
            var loadBalancer = await TryCreateLoadBalancer();
            loadBalancer.ShouldBeNull();            
        }


        [Test]
        public async Task TryCreateNodeSource_ReturnNullIfServiceUndeployed()
        {
            ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            var source = await TryCreateNodeSource();
            source.ShouldBeNull();
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnNullIfServiceUndeployed()
        {
            ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            var loadBalancer = await TryCreateLoadBalancer();
            loadBalancer.ShouldBeNull();
        }

        private void ConfigureServiceSource(string source)
        {
            _discoveryConfig.Services[ServiceName].Source = source;
            if (source==Config)
                _discoveryConfig.Services[ServiceName].Hosts = "myhost";
        }

        private Task<INodeSource> TryCreateNodeSource(string serviceName = ServiceName, string env = Env)
        {
            return _factory.TryCreateNodeSource(new DeploymentIdentifier(serviceName, env));            
        }

        private Task<ILoadBalancer> TryCreateLoadBalancer(string serviceName = ServiceName, string env = Env)
        {
            return _factory.TryCreateLoadBalancer(new DeploymentIdentifier(serviceName, env), null);
        }

    }
}
