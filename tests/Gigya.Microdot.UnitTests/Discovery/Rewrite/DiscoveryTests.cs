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
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class DiscoveryTests : UpdatableConfigTests
    {
        private const string Consul = "Consul";
        private const string SlowSource = "SlowSource";
        private const string Config = "Config";
        private const string Local = "Local";

        private const string ServiceName = "ServiceName";
        private const string Env = "env";

        private IDiscovery _discovery;

        private bool _consulSourceWasUndeployed;
        private List<Type> _createdNodeSources;
        private DeploymentIdentifier _deploymentIdentifier;

        private Node _consulNode;        
        private INodeSourceFactory _consulNodeSourceFactory;

        private INodeSource _slowNodeSource;
        private INodeSourceFactory _slowNodeSourceFactory;
        private TaskCompletionSource<bool> _waitForSlowSourceCreation;
        private DateTimeFake _dateTimeFake;
        private int _consulSourceDisposedCounter;


        protected override Action<IKernel> AdditionalBindings()
        {
            return k =>
                    {
                        RebindKernelToSetCreatedNodeSourceBeforeCreatingIt<LocalNodeSource>(k);
                        RebindKernelToSetCreatedNodeSourceBeforeCreatingIt<ConfigNodeSource>(k);

                        k.Rebind<INodeSourceFactory>().ToMethod(c => _consulNodeSourceFactory);
                        k.Bind<INodeSourceFactory>().ToMethod(c => _slowNodeSourceFactory);
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InTransientScope(); // get a different instance for each test
                        k.Rebind<IDateTime>().ToMethod(_ => _dateTimeFake);
                    };
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

        public override void OneTimeSetUp()
        {
            
        }

        public override void Setup()
        {
            base.Setup();
            _dateTimeFake = new DateTimeFake();

            _createdNodeSources = new List<Type>();
            SetupConsulNodeSource();
            SetupSlowNodeSource();
            
            _discovery = _unitTestingKernel.Get<IDiscovery>();
            _deploymentIdentifier = new DeploymentIdentifier(ServiceName, Env, Substitute.For<IEnvironment>());            
        }

        public override void TearDown()
        {
            base.TearDown();

            ClearChanges<DiscoveryConfig>();
        }

        private void SetupConsulNodeSource()
        {
            _consulSourceDisposedCounter = 0;

            _consulNode = new Node("ConsulNode", 123);
            _consulSourceWasUndeployed = false;            

            _consulNodeSourceFactory = Substitute.For<INodeSourceFactory>();
            _consulNodeSourceFactory.Type.Returns(Consul);
            _consulNodeSourceFactory.IsServiceDeployed(Arg.Any<DeploymentIdentifier>()).Returns(c=> !_consulSourceWasUndeployed);
            _consulNodeSourceFactory.CreateNodeSource(Arg.Any<DeploymentIdentifier>())
                .Returns(_ =>
                {
                    _createdNodeSources.Add(typeof(INodeSource));
                    return _consulSourceWasUndeployed ? null : CreateNewConsulSource(); 
                });
        }

        private INodeSource CreateNewConsulSource()
        {
            var consulSource = Substitute.For<INodeSource, IDisposable>();
            consulSource.GetNodes().Returns(new[] {_consulNode});            
            consulSource.When(n => ((IDisposable) n).Dispose()).Do(_ => _consulSourceDisposedCounter++);
            return consulSource;
        }

        private void SetupSlowNodeSource()
        {
            _waitForSlowSourceCreation = new TaskCompletionSource<bool>();

            _slowNodeSource = Substitute.For<INodeSource>();
            _slowNodeSource.GetNodes().Returns(new Node[0] );

            _slowNodeSourceFactory = Substitute.For<INodeSourceFactory>();
            _slowNodeSourceFactory.Type.Returns(SlowSource);
            _slowNodeSourceFactory.IsServiceDeployed(Arg.Any<DeploymentIdentifier>()).Returns(true);
            _slowNodeSourceFactory.CreateNodeSource(Arg.Any<DeploymentIdentifier>())
                .Returns(async _ =>
                {
                    _createdNodeSources.Add(typeof(INodeSource));
                    await Task.WhenAny(_waitForSlowSourceCreation.Task, Task.Delay(5000));
                    return _slowNodeSource;
                });
        }

        [Test]
        public async Task CreateLoadBalancer_GetNodesFromConsulNodeSource()
        {
            await ConfigureServiceSource(Consul);
            var loadBalancer = CreateLoadBalancer();
            (await loadBalancer.TryGetNode()).ShouldBe(_consulNode);            
        }

        [Test]
        public async Task CreateLoadBalancer_GetNodesFromConfigNodeSource()
        {
            await ConfigureServiceSource(Config);
            await CreateLoadBalancer().TryGetNode();
            _createdNodeSources.Single().ShouldBe(typeof(ConfigNodeSource));
        }

        [Test]
        public async Task CreateLoadBalancer_GetNodesFromLocalNodeSource()
        {
            await ConfigureServiceSource(Local);
            await CreateLoadBalancer().TryGetNode();
            _createdNodeSources.Single().ShouldBe(typeof(LocalNodeSource));
        }

        [Test]
        public async Task CreateLoadBalancer_ReturnNullIfServiceUndeployed()
        {
            await ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            var loadBalancer = CreateLoadBalancer();
            (await loadBalancer.TryGetNode()).ShouldBeNull();
        }

        [Test]
        public async Task GetNodes_SourceWasUndeployed_ReturnNull()
        {
            await ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            (await GetNodes()).ShouldBeNull();
        }

        [Test]
        public async Task GetNodes_SourceWasRedeployed_ReturnNodes()
        {
            await ConfigureServiceSource(Consul);
            _consulSourceWasUndeployed = true;
            await GetNodes();
            _consulSourceWasUndeployed = false;
            (await GetNodes()).ShouldContain(_consulNode);
        }

        [Test]
        public async Task GetNodes_ConfigurationChanged_ReturnNodesFromNewNodeSource()
        {
            ClearChanges<DiscoveryConfig>();
            await ConfigureServiceSource(Local);            
            (await GetNodes()).ShouldNotContain(_consulNode);

            await ConfigureServiceSource(Consul);
            await WaitForCleanup();
            (await GetNodes()).ShouldContain(_consulNode);
        }

        [Test]
        public async Task GetNodes_CalledMoreThanOnce_OnlyOneNodeSourceIsCreated()
        {
            await ConfigureServiceSource(SlowSource);
            await GetNodesThreeTimesFromSlowSource();
            _createdNodeSources.Count.ShouldBe(1);
        }

        [Test]
        public async Task GetNodes_CalledMoreThanOnceAfterConfigurationChanged_OnlyOneAdditionalNodeSourceIsCreated()
        {
            await ConfigureServiceSource(Local);
            await GetNodes();
            _createdNodeSources.Count.ShouldBe(1);

            await ConfigureServiceSource(SlowSource);
            await WaitForCleanup();
            await GetNodesThreeTimesFromSlowSource();
            _createdNodeSources.Count.ShouldBe(2);
        }

        [Test]
        public async Task GetNodes_ServiceUndeployed_DontTryToCreateNewNodeSourceUntilServiceIsRedeployed()
        {
            await ConfigureServiceSource(Consul);
            await GetNodes();
            _createdNodeSources.Count.ShouldBe(1);

            _consulSourceWasUndeployed = true;
            await WaitForCleanup();
            (await GetNodes()).ShouldBeNull();            
            _createdNodeSources.Count.ShouldBe(1);

            _consulSourceWasUndeployed = false;
            await WaitForCleanup();
            (await GetNodes()).ShouldNotBeNull();
            _createdNodeSources.Count.ShouldBe(2);
        }

        [Test]
        public async Task DisposeNodeSourceAfterLifetimeIsPassed()
        {
            await ConfigureServiceSource(Consul);
            await ChangeConfig<DiscoveryConfig>(new[]
                            {
                                new KeyValuePair<string, string>($"Discovery.MonitoringLifetime", TimeSpan.FromMinutes(2).ToString())
                            });
            
            await GetNodes();
            _createdNodeSources.Count.ShouldBe(1);

            _dateTimeFake.UtcNow += TimeSpan.FromMinutes(3);
            await WaitForCleanup();
            await GetNodes();
            // first NodeSource was disposed after being not-in-use for more than 2 minutes. A new NodeSource should have been created
            _createdNodeSources.Count.ShouldBe(2);
            _consulSourceDisposedCounter.ShouldBe(1);
        }

        private async Task WaitForCleanup()
        {
            await Task.Delay(100);
            _dateTimeFake.StopDelay();
            await Task.Delay(100);
        }

        private async Task ConfigureServiceSource(string sourceType)
        {
            List<KeyValuePair<string, string>> configUpdates = new[]
                                                                    {
                                                                        new KeyValuePair<string, string>($"Discovery.Services.{_deploymentIdentifier.ServiceName}.Source",
                                                                            sourceType)
                                                                    }.ToList();
            if (sourceType == Config)
                configUpdates.Add(new KeyValuePair<string, string>($"Discovery.Services.{_deploymentIdentifier.ServiceName}.Hosts", "myhost"));

            await ChangeConfig<DiscoveryConfig>(configUpdates);
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

        private ILoadBalancer CreateLoadBalancer()
        {
            return _discovery.CreateLoadBalancer(_deploymentIdentifier, null, TrafficRoutingStrategy.RandomByRequestID);
        }

        private async Task<Node[]> GetNodes()
        {
            return await _discovery.GetNodes(_deploymentIdentifier);
        }
    }
}
