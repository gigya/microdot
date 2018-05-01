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

        private const string ServiceName = "ServiceName";
        private const string Env = "env";

        private IDiscoveryFactory _factory;

        private TestingKernel<ConsoleLog> _kernel;
        private INodeSource[] _nodeSources;
        private INodeSource _consulSource;
        private INodeSource _configSource;
        private ILoadBalancer _consulLoadBalancer;
        private ILoadBalancer _configLoadBalancer;
        private bool _consulSourceWasUndeployed;
        private bool _consulSourceInitiated = false;
        private DiscoveryConfig _discoveryConfig;        


        [OneTimeSetUp]
        public void SetupKernel()
        {
            _kernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<Func<DeploymentIdentifier, INodeSource[]>>().ToMethod(c => i => _nodeSources);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(c=> () => _discoveryConfig);
                k.Rebind<IDiscoveryFactory>().To<DiscoveryFactory>().InSingletonScope();
                k.Rebind<Func<DeploymentIdentifier, INodeSource, ReachabilityCheck, ILoadBalancer>>()
                .ToMethod(c=> (di, source, rc)=>
                {                    
                    if (source == _consulSource)
                        return _consulLoadBalancer;
                    else if (source == _configSource)
                        return _configLoadBalancer;
                    else
                        throw new Exception("Cannot create loadBalancer for unknown source");
                });
            });
        }

        [SetUp]
        public void Setup()
        {
            _consulSourceInitiated = false;
            _consulSourceWasUndeployed = false;
            _consulSource = Substitute.For<INodeSource>();
            _configSource = Substitute.For<INodeSource>();
            _consulSource.Type.Returns(Consul);
            _configSource.Type.Returns(Config);
            _consulSource.SupportsMultipleEnvironments.Returns(true);
            _configSource.SupportsMultipleEnvironments.Returns(false);
            _consulSource.WasUndeployed.Returns(_ => _consulSourceWasUndeployed);
            _consulSource.Init().Returns(_ =>
            {
                _consulSourceInitiated = true;
                return Task.FromResult(true);
            });

            _nodeSources = new []{_consulSource, _configSource};

            _consulLoadBalancer = Substitute.For<ILoadBalancer>();
            _configLoadBalancer = Substitute.For<ILoadBalancer>();
            
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
            _consulSourceInitiated.ShouldBeTrue();            
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnConsulLoadBalancer()
        {
            ConfigureServiceSource(Consul);
            var loadBalancer = await TryCreateLoadBalancer();
            loadBalancer.ShouldBe(_consulLoadBalancer);
            _consulSourceInitiated.ShouldBeTrue();
        }

        [Test]
        public async Task TryCreateNodeSource_ReturnConfigSource()
        {
            ConfigureServiceSource(Config);
            var source = await TryCreateNodeSource(env: "prod");
            source.ShouldBe(_configSource);   
            _consulSourceInitiated.ShouldBeFalse();
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnConfigLoadBalancer()
        {
            ConfigureServiceSource(Config);
            var loadBalancer = await TryCreateLoadBalancer(env: "prod");
            loadBalancer.ShouldBe(_configLoadBalancer);
            _consulSourceInitiated.ShouldBeFalse();
        }

        [Test]
        public async Task TryCreateNodeSource_NotLastFallbackEnvironment_ReturnNull()
        {
            ConfigureServiceSource(Config);
            var source = await TryCreateNodeSource(env: Env);
            source.ShouldBeNull();            
        }

        [Test]
        public async Task TryCreateLoadBalancer_NotLastFallbackEnvironment_ReturnNull()
        {
            ConfigureServiceSource(Config);
            var loadBalancer = await TryCreateLoadBalancer(env: Env);
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
