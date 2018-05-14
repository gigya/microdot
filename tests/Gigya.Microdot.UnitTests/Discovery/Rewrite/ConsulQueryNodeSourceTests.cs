using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
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
using Node = Gigya.Microdot.ServiceDiscovery.Rewrite.Node;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    public class ConsulQueryNodeSourceTests
    {
        private const string Host1 = "Host1";
        private const int Port1 = 1234;
              
        private readonly DeploymentIdentifier _deployment = new DeploymentIdentifier("MyService", "prod");

        private TestingKernel<ConsoleLog> _testingKernel;
        private INodeSource _consulSource;
        private ConsulConfig _consulConfig;
        private INodeMonitor _queryBasedConsulNodeMonitor;
        private INode[] _consulNodes;
        private Func<INode[]> _getConsulNodes;
        private bool _serviceIsDeployed;

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<INodeMonitor>().ToMethod(_ => _queryBasedConsulNodeMonitor);
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
            _consulNodes = new INode[0];
            _serviceIsDeployed = true;
            _getConsulNodes = () => _consulNodes;
            _queryBasedConsulNodeMonitor = Substitute.For<INodeMonitor>();
            _queryBasedConsulNodeMonitor.Nodes.Returns(_=>_getConsulNodes());
            _queryBasedConsulNodeMonitor.WasUndeployed.Returns(_ => !_serviceIsDeployed);
            _consulConfig = new ConsulConfig();
        }

        [TearDown]
        public void Teardown()
        {
            _consulSource?.Dispose();
        }

        [Test]
        public void GetNodes()
        {
            SetupOneDefaultNode();

            Start();
            
            AssertOneDefaultNode();
        }

        [Test]
        public void ServiceNotDeployedOnConsul_ReturnWasUndeployedTrue()
        {
            _serviceIsDeployed = false;
            Start();
            _consulSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceBecomesNotDeployedOnConsul_ReturnIsActiveFalse()
        {
            SetupOneDefaultNode();
            await Start();
            _serviceIsDeployed = false;
            _consulSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public void ConsulErrorOnStart_ThrowErrorWhenGettingNodesList()
        {
            SetupConsulError();
            Start();
            _consulSource.WasUndeployed.ShouldBeFalse();
            Should.Throw<EnvironmentException>(()=> _consulSource.GetNodes());
        }

        [Test]
        public async Task InitMonitorsOnlyAfterInitAndNotOnCreation()
        {
            bool nodesMonitorInitiated = false;

            _queryBasedConsulNodeMonitor.Init().Returns(_=>{
                                                    nodesMonitorInitiated = true;
                                                    return Task.FromResult(1);});

            CreateConsulSource();
            await Task.Delay(200);
            nodesMonitorInitiated.ShouldBeFalse("QueryBasedConsulNodeMonitor.Init() was called before Init() was called");
            await _consulSource.Init();
            nodesMonitorInitiated.ShouldBeTrue("QueryBasedConsulNodeMonitor.Init() should be called when Init() is called");
        }

        [Test]
        public void SupportMultipleEnvironments()
        {
            CreateConsulSource();
            _consulSource.SupportsMultipleEnvironments.ShouldBeTrue();
        }

        private void SetupOneDefaultNode()
        {
            _consulNodes = new[] { new Node(Host1, Port1)};
        }

        private void SetupConsulError()
        {
            _getConsulNodes = () => throw new EnvironmentException("Error on Consul");
        }

        private async Task Start()
        {
            CreateConsulSource();
            await _consulSource.Init();
        }

        private void CreateConsulSource()
        {            
            var sources = _testingKernel.Get<Func<DeploymentIdentifier, INodeSource[]>>()(_deployment);
            _consulSource = sources.Single(x => x.Type == "ConsulQuery");            
        }

        private void AssertOneDefaultNode()
        {
            _consulSource.WasUndeployed.ShouldBeFalse();
            var nodes = _consulSource.GetNodes();
            nodes.Length.ShouldBe(1);
            nodes[0].Hostname.ShouldBe(Host1);
            nodes[0].Port.ShouldBe(Port1);
        }
    }
}
