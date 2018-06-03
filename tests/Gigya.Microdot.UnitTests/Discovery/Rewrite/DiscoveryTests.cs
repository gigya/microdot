using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class DiscoveryTests
    {
        private const string Consul = "Consul";
        private const string ConsulQuery = "ConsulQuery";
        private const string SlowSource = "SlowSource";
        private const string Config = "Config";
        private const string Local = "Local";

        private const string ServiceName = "ServiceName";
        private const string Env = "env";

        private IDiscovery _discovery;

        private TestingKernel<ConsoleLog> _kernel;
        private bool _consulSourceWasUndeployed;
        private DiscoveryConfig _discoveryConfig;
        private List<Type> _createdNodeSources;
        private DeploymentIdentifier _deploymentIdentifier;

        private Node _consulNode;
        private INodeSource _consulSource;
        private INodeSourceFactory _consulNodeSourceFactory;

        private INodeSource _slowNodeSource;
        private INodeSourceFactory _slowNodeSourceFactory;
        private TaskCompletionSource<bool> _waitForSlowSourceCreation;
        private DateTimeFake _dateTimeFake;
        private int _consulSourceDisposedCounter;


        [OneTimeSetUp]
        public void SetupKernel()
        {
            _kernel = new TestingKernel<ConsoleLog>(k =>
            {
                RebindKernelToSetCreatedNodeSourceBeforeCreatingIt<LocalNodeSource>(k);
                RebindKernelToSetCreatedNodeSourceBeforeCreatingIt<ConfigNodeSource>(k);
                RebindKernelToSetCreatedNodeSourceBeforeCreatingIt<ConsulQueryNodeSource>(k);

                k.Rebind<INodeSourceFactory>().ToMethod(c => _consulNodeSourceFactory);
                k.Bind<INodeSourceFactory>().ToMethod(c => _slowNodeSourceFactory);
                k.Rebind<Func<DiscoveryConfig>>().ToMethod(c => () => _discoveryConfig);                
                k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InTransientScope(); // get a different instance for each test
                k.Rebind<IDateTime>().ToMethod(_=>_dateTimeFake);
            });
        }

        [OneTimeTearDown]
        public void DisposeKernel()
        {
            _kernel.Dispose();
        }

        private void RebindKernelToSetCreatedNodeSourceBeforeCreatingIt<TNodeSource>(IKernel kernel) where TNodeSource: INodeSource
        {
            var createLocalNodeSource = kernel.Get<Func<DeploymentIdentifier, TNodeSource>>();
            kernel.Rebind<Func<DeploymentIdentifier, TNodeSource>>().ToMethod(_ => di=>
            {
                _createdNodeSources.Add(typeof(TNodeSource));
                return createLocalNodeSource(di);
            });
        }

        [SetUp]
        public void Setup()
        {
            _dateTimeFake = new DateTimeFake();

            _createdNodeSources = new List<Type>();
            SetupConsulNodeSource();
            SetupSlowNodeSource();

            _discoveryConfig = new DiscoveryConfig();
            _discoveryConfig.Services = new ServiceDiscoveryCollection(new Dictionary<string, ServiceDiscoveryConfig>(), new ServiceDiscoveryConfig(), new PortAllocationConfig());

            _discovery = _kernel.Get<IDiscovery>();
            _deploymentIdentifier = new DeploymentIdentifier(ServiceName, Env);            
        }

        private void SetupConsulNodeSource()
        {
            _consulSourceDisposedCounter = 0;

            _consulNode = new Node("ConsulNode", 123);
            _consulSourceWasUndeployed = false;
            _consulSource = Substitute.For<INodeSource, IDisposable>();
            _consulSource.WasUndeployed.Returns(_ => _consulSourceWasUndeployed);
            _consulSource.GetNodes().Returns(new[] {_consulNode});
            _consulSource.Type.Returns(Consul);  
            _consulSource.When(n=>((IDisposable)n).Dispose()).Do(_=>_consulSourceDisposedCounter++);

            _consulNodeSourceFactory = Substitute.For<INodeSourceFactory>();
            _consulNodeSourceFactory.Type.Returns(Consul);
            _consulNodeSourceFactory.TryCreateNodeSource(Arg.Any<DeploymentIdentifier>())
                .Returns(_ =>
                {
                    _createdNodeSources.Add(typeof(INodeSource));
                    return _consulSourceWasUndeployed ? null : _consulSource;
                });
        }

        private void SetupSlowNodeSource()
        {
            _waitForSlowSourceCreation = new TaskCompletionSource<bool>();

            _slowNodeSource = Substitute.For<INodeSource>();
            _slowNodeSource.WasUndeployed.Returns(false);
            _slowNodeSource.GetNodes().Returns(new Node[0] );
            _slowNodeSource.Type.Returns(SlowSource);

            _slowNodeSourceFactory = Substitute.For<INodeSourceFactory>();
            _slowNodeSourceFactory.Type.Returns(SlowSource);
            _slowNodeSourceFactory.TryCreateNodeSource(Arg.Any<DeploymentIdentifier>())
                .Returns(async _ =>
                {
                    _createdNodeSources.Add(typeof(INodeSource));
                    await Task.WhenAny(_waitForSlowSourceCreation.Task, Task.Delay(5000));
                    return _slowNodeSource;
                });
        }

        [Test]
        public async Task TryCreateLoadBalancer_GetNodesFromConsulNodeSource()
        {
            ConfigureServiceSource(Consul);
            var loadBalancer = await TryCreateLoadBalancer();
            (await loadBalancer.GetNode()).ShouldBe(_consulNode);            
        }

        [Test]
        public async Task TryCreateLoadBalancer_GetNodesFromConfigNodeSource()
        {
            ConfigureServiceSource(Config);
            await TryCreateLoadBalancer();
            _createdNodeSources.Single().ShouldBe(typeof(ConfigNodeSource));
        }

        [Test]
        public async Task TryCreateLoadBalancer_GetNodesFromConsulQueryNodeSource()
        {
            ConfigureServiceSource(ConsulQuery);
            await TryCreateLoadBalancer();
            _createdNodeSources.Single().ShouldBe(typeof(ConsulQueryNodeSource));
        }

        [Test]
        public async Task TryCreateLoadBalancer_GetNodesFromLocalNodeSource()
        {
            ConfigureServiceSource(Local);
            await TryCreateLoadBalancer();
            _createdNodeSources.Single().ShouldBe(typeof(LocalNodeSource));
        }

        [Test]
        public async Task TryCreateLoadBalancer_ReturnNullIfServiceUndeployed()
        {
            ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            var loadBalancer = await TryCreateLoadBalancer();
            loadBalancer.ShouldBeNull();
        }

        [Test]
        public async Task GetNodes_SourceWasUndeployed_ReturnNull()
        {
            ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            (await GetNodes()).ShouldBeNull();
        }

        [Test]
        public async Task GetNodes_SourceWasRedeployed_ReturnNodes()
        {
            ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            await GetNodes();
            _consulSourceWasUndeployed = false;
            (await GetNodes()).ShouldContain(_consulNode);
        }

        [Test]
        public async Task GetNodes_ConfigurationChanged_ReturnNodesFromNewNodeSource()
        {
            ConfigureServiceSource(Local);            
            (await GetNodes()).ShouldNotContain(_consulNode);

            ConfigureServiceSource(Consul);
            (await GetNodes()).ShouldContain(_consulNode);
        }

        [Test]
        public async Task GetNodes_CalledMoreThanOnce_OnlyOneNodeSourceIsCreated()
        {
            ConfigureServiceSource(SlowSource);
            await GetNodesThreeTimesFromSlowSource();
            _createdNodeSources.Count.ShouldBe(1);
        }

        [Test]
        public async Task GetNodes_CalledMoreThanOnceAfterConfigurationChanged_OnlyOneAdditionalNodeSourceIsCreated()
        {
            ConfigureServiceSource(Local);
            await GetNodes();
            _createdNodeSources.Count.ShouldBe(1);

            ConfigureServiceSource(SlowSource);
            await GetNodesThreeTimesFromSlowSource();
            _createdNodeSources.Count.ShouldBe(2);
        }

        [Test]
        public async Task DisposeNodeSourceAfterLifetimeIsPassed()
        {
            ConfigureServiceSource(Consul);
            _discoveryConfig.MonitoringLifetime = TimeSpan.FromMinutes(2);
            await GetNodes();
            _createdNodeSources.Count.ShouldBe(1);

            _dateTimeFake.UtcNow += TimeSpan.FromMinutes(3);
            _dateTimeFake.StopDelay();
            await Task.Delay(100);
            await GetNodes();
            // first NodeSource was disposed after being not-in-use for more than 2 minutes. A new NodeSource should have been created
            _createdNodeSources.Count.ShouldBe(2);
            _consulSourceDisposedCounter.ShouldBe(1);
        }

        private void ConfigureServiceSource(string source)
        {
            _discoveryConfig.Services[ServiceName].Source = source;
            if (source == Config)
                _discoveryConfig.Services[ServiceName].Hosts = "myhost";
        }

        private async Task GetNodesThreeTimesFromSlowSource()
        {
            var getNodesTasks = Task.WhenAll(GetNodes(), GetNodes(), GetNodes());
            await Task.Delay(100);
            SlowSourceCanFinallyBeCreated();
            await getNodesTasks;
        }

        private void SlowSourceCanFinallyBeCreated()
        {
            _waitForSlowSourceCreation.SetResult(true);
        }

        private Task<ILoadBalancer> TryCreateLoadBalancer()
        {
            return _discovery.TryCreateLoadBalancer(_deploymentIdentifier, null);
        }

        private async Task<Node[]> GetNodes()
        {
            return await _discovery.GetNodes(_deploymentIdentifier);
        }
    }
}
