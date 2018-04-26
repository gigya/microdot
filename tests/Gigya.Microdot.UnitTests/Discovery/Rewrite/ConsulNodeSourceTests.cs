using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
    public class ConsulNodeSourceTests
    {
        private const string Host1 = "Host1";
        private const int Port1 = 1234;
              
        private readonly DeploymentIdentifier _deployment = new DeploymentIdentifier("MyService", "prod");

        private TestingKernel<ConsoleLog> _testingKernel;
        private INodeSource _consulSource;
        private ConsulConfig _consulConfig;
        private INodeMonitor _nodeMonitor;
        private INode[] _consulNodes;
        private HashSet<string> _consulServicesList;
        private IServiceListMonitor _serviceListMonitor;
        private Func<INode[]> _getConsulNodes;
        private string _serviceName;

        [OneTimeSetUp]
        public void SetupConsulListener()
        {
            _testingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<Func<string, INodeMonitor>>().ToConstant<Func<string,INodeMonitor>>(s =>
                {
                    _serviceName = s;
                    return _nodeMonitor;
                });
                k.Rebind<IServiceListMonitor>().ToMethod(_ => _serviceListMonitor);
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
            _getConsulNodes = () => _consulNodes;
            _consulServicesList = new HashSet<string>(new[]{_deployment.ToString()});
            _nodeMonitor = Substitute.For<INodeMonitor>();
            _nodeMonitor.Nodes.Returns(_ => _getConsulNodes());
            _nodeMonitor.IsDeployed.Returns(_ => _consulServicesList.Contains(_serviceName));
            _serviceListMonitor = Substitute.For<IServiceListMonitor>();
            _serviceListMonitor.Services.Returns(_ => _consulServicesList.ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase));

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
        public void ServiceNotDeployedOnConsul_ReturnWasUndeployedTrue()
        {
            _consulServicesList.Clear();
            Start();
            _consulSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceBecomesNotDeployedOnConsul_ReturnWasUndeployedTrue()
        {
            SetupOneDefaultNode();
            await Start();
            _consulServicesList.Clear();            
            _consulSource.WasUndeployed.ShouldBeTrue();
        }

        [Test]
        public async Task ServiceIsDeployedInLowerCase()
        {
            var lowerCaseServiceName = _deployment.ToString().ToLower();
            _consulServicesList = new HashSet<string>(new[] { lowerCaseServiceName });
            SetupOneDefaultNode();

            await Start();

            AssertOneDefaultNode();
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
            bool serviceListMonitorInitiated = false;

            _nodeMonitor.Init().Returns(_=>{
                                                    nodesMonitorInitiated = true;
                                                    return Task.FromResult(1);});
            _serviceListMonitor.Init().Returns(_ => {
                                                    serviceListMonitorInitiated = true;
                                                    return Task.FromResult(1);
                                                });

            CreateConsulSource();
            await Task.Delay(200);
            nodesMonitorInitiated.ShouldBeFalse("ConsulNodeMonitor.Init() was called before Init() was called");
            serviceListMonitorInitiated.ShouldBeFalse("ConsulServiceListMonitor.Init() was called before Init() was called");
            await _consulSource.Init();
            nodesMonitorInitiated.ShouldBeTrue("ConsulNodeMonitor.Init() should be called when Init() is called");
            serviceListMonitorInitiated.ShouldBeTrue("ConsulServiceListMonitor.Init() should be called when Init() is called");
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
    }
}
