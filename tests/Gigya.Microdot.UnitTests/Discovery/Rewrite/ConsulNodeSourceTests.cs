using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using ServiceDeployment = Gigya.Microdot.ServiceDiscovery.ServiceDeployment;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    public class ConsulNodeSourceTests
    {
        private const string Host1 = "Host1";
        private const int Port1 = 1234;
        private const string Version = "1.0.0.1";

        private const string Host2 = "Host2";
        private const int Port2 = 5678;
        private const string Version2 = "2.0.0.1";

        private TestingKernel<ConsoleLog> _testingKernel;
        private IConsulClient _consulClient;
        private Action<ConsulServiceState> _consulClientResult;
        private INodeSource _consulSource;
        private ConsulConfig _consulConfig;

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IConsulClient>().ToMethod(_ => _consulClient);
                k.Rebind<Func<ConsulConfig>>().ToMethod(_ => ()=>_consulConfig);
            });
        }

        [OneTimeTearDown]
        public void TearDownConsulListener()
        {
            _testingKernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _consulClientResult = _ => { };
            _consulClient = Substitute.For<IConsulClient>();
            _consulClient.LoadNodes(Arg.Any<ConsulServiceState>())
                .Returns(async c =>
                {
                    var serviceState = c.Arg<ConsulServiceState>();
                    _consulClientResult.Invoke(serviceState);
                    await Task.Delay(100); // simulate a small delay for response from Consul
                });
            _consulConfig = new ConsulConfig();
        }

        [TearDown]
        public void Teardown()
        {
            _consulSource.Dispose();
        }

        [Test]
        public void GetNodes()
        {
            SetupOneDefaultNode();

            Start();
            
            AssertOneDefaultNode();
        }

        [Test]
        public void GetOnlyNodesOfCurrentVersion()
        {
            _consulClientResult = s =>
            {
                s.NodesOfAllVersions = new[] { new Node(Host1, Port1, Version), new Node(Host2, Port2, Version2) };
                s.ActiveVersion = Version;
            };

            Start();

            AssertOneDefaultNode();
        }

        [Test]
        public void ServiceNotDeployedOnConsul_ReturnIsActiveFalse()
        {
            _consulClientResult = s => { s.IsDeployed = false; };
            CreateConsulSource();
            _consulSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceBecomesNotDeployedOnConsul_ReturnIsActiveFalse()
        {
            SetupOneDefaultNode();
            await Start();
            await SetupConsulResult(s => { s.IsDeployed = false; });            
            _consulSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public void ConsulErrorOnStart_ReturnEmptyNodesList()
        {
            SetupConsulError();
            Start();
            _consulSource.WasUndeployed.ShouldBeFalse();
            _consulSource.GetNodes().ShouldBeEmpty();
        }

        [Test]
        public async Task ConsulError_UseLastKnownResult()
        {
            SetupOneDefaultNode();
            await Start();
            await SetupConsulError();
            AssertOneDefaultNode();
        }

        [Test]
        public async Task GetResultAfterConsulError_UseLastKnownResult()
        {
            SetupOneDefaultNode();
            await Start();
            await SetupConsulError();
            await SetupConsulResult(s => s.Nodes = new INode[]{new Node(Host1, Port1)});
            AssertOneDefaultNode();
        }

        [Test]
        public async Task CallConsulClientOnlyAfterInitAndNotOnCreation()
        {
            bool consulClientCalled = false;
            _consulClient = Substitute.For<IConsulClient>();
            _consulClient.LoadNodes(Arg.Any<ConsulServiceState>()).Returns(async _=>
                                                                    {
                                                                        consulClientCalled = true;
                                                                        await Task.Delay(10);
                                                                    });
            CreateConsulSource();
            await Task.Delay(200);
            consulClientCalled.ShouldBeFalse("ConsulClient was called before GetNodes() was requested");
            await _consulSource.Init();
            consulClientCalled.ShouldBeTrue("ConsulClient should be called when GetNodes() is requested");
        }

        [Test]
        public void WaitForConsulResultBeforeFirstGetNodes()
        {
            var consulInitializationDelay = TimeSpan.FromMilliseconds(200);            
            SetupConsulDelay(consulInitializationDelay);

            AssertActionTakesAtLeast(consulInitializationDelay, () =>
            {
                Start();
                _consulSource.GetNodes();
            });
        }

        [Test]
        public void SupportMultipleEnvironments()
        {
            CreateConsulSource();
            _consulSource.SupportsMultipleEnvironments.ShouldBeTrue();
        }

        private void SetupOneDefaultNode()
        {
            _consulClientResult = s =>
            {
                s.NodesOfAllVersions = new[] { new Node(Host1, Port1, Version) };
                s.ActiveVersion = Version;
            };
        }

        private async Task SetupConsulResult(Action<ConsulServiceState> setConsulResult)
        {
            var resultAcceptedByConsulClient = new TaskCompletionSource<bool>();

            _consulClientResult = serviceState =>
            {
                setConsulResult(serviceState);
                resultAcceptedByConsulClient.TrySetResult(true);
            };
            await resultAcceptedByConsulClient.Task;
            await Task.Delay(200);
        }

        private Task SetupConsulError()
        {
            return SetupConsulResult(s =>
            {
                s.LastResult = new ConsulResult {Error = new Exception("Error on consul")};
                s.Nodes = new INode[0];
                s.IsDeployed = true;
            });
        }

        private void SetupConsulDelay(TimeSpan consulInitializationDelay)
        {
            _consulClientResult = s =>
            {
                Task.Delay(consulInitializationDelay).Wait();
                s.NodesOfAllVersions = new[] {new Node(Host1, Port1)};
            };
        }

        private async Task Start()
        {
            CreateConsulSource();
            await _consulSource.Init();
        }

        private void CreateConsulSource()
        {
            var deployment = new ServiceDeployment("MyService", "prod");

            var sources = _testingKernel.Get<Func<ServiceDeployment, INodeSource[]>>()(deployment);
            _consulSource = sources.Single(x => x.Type == "Consul");
        }

        private void AssertOneDefaultNode()
        {
            _consulSource.WasUndeployed.ShouldBeFalse();
            var nodes = _consulSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe(Host1);
            nodes[0].Port.ShouldBe(Port1);
        }

        private void AssertActionTakesAtLeast(TimeSpan consulInitializationDelay, Action action)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            action.Invoke();
            stopwatch.Stop();
            stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(consulInitializationDelay);
        }
    }
}
