using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Utils;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Node = Gigya.Microdot.ServiceDiscovery.Rewrite.Node;

namespace Gigya.Microdot.UnitTests.Discovery.Rewrite
{
    [TestFixture]
    public class NewConsulDiscoveryMasterFallBackTest
    {
        private const string ServiceVersion = "1.2.30.1234";
        private string _serviceName;
        private const string MASTER_ENVIRONMENT = "prod";
        private const string ORIGINATING_ENVIRONMENT = "fake_env";
        private readonly TimeSpan _timeOut = TimeSpan.FromSeconds(5);
        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private Dictionary<string, INodeMonitor> _consulNodeMonitors;
        private Dictionary<string, Func<INode[]>> _consulNodesResults;
        private IConsulServiceListMonitor _consulConsulServiceListMonitor;
        private ImmutableHashSet<string> _consulServiceList;
        private int _serviceListVersion = 0;
        private IEnvironmentVariableProvider _environmentVariableProvider;
        private ManualConfigurationEvents _configRefresh;
        private IDateTime _dateTimeMock;
        private int id;
        private const int Repeat = 1;

        [SetUp]
        public void SetUp()
        {
            _unitTestingKernel?.Dispose();
            _serviceName = $"ServiceName{++id}";

            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(ORIGINATING_ENVIRONMENT);
            _environmentVariableProvider.ConsulAddress.Returns((string)null);

            _configDic = new Dictionary<string, string> {{"Discovery.EnvironmentFallbackEnabled", "true"}};
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);
                k.Rebind<IDiscoveryFactory>().To<DiscoveryFactory>().InSingletonScope();

                SetupConsulMocks(k);

                _dateTimeMock = Substitute.For<IDateTime>();
                _dateTimeMock.Delay(Arg.Any<TimeSpan>()).Returns(c => Task.Delay(TimeSpan.FromMilliseconds(100)));
                k.Rebind<IDateTime>().ToConstant(_dateTimeMock);
            }, _configDic);
            _configRefresh = _unitTestingKernel.Get<ManualConfigurationEvents>();

            var environmentVariableProvider = _unitTestingKernel.Get<IEnvironmentVariableProvider>();
            Assert.AreEqual(_environmentVariableProvider, environmentVariableProvider);
        }

        private void SetupConsulMocks(IKernel kernel)
        {
            _consulNodeMonitors = new Dictionary<string, INodeMonitor>();
            _consulNodesResults = new Dictionary<string, Func<INode[]>>();

            _consulConsulServiceListMonitor = Substitute.For<IConsulServiceListMonitor>();
            _consulServiceList = new HashSet<string>().ToImmutableHashSet();
            _consulConsulServiceListMonitor.Services.Returns(_ => _consulServiceList);
            _consulConsulServiceListMonitor.Version.Returns(_ => _serviceListVersion);

            kernel.Rebind<Func<string, INodeMonitor>>().ToMethod(_ => (s => _consulNodeMonitors[s]));
            kernel.Rebind<IConsulServiceListMonitor>().ToMethod(_ => _consulConsulServiceListMonitor);

            CreateConsulMock(MasterService);
            CreateConsulMock(OriginatingService);

        }

        private void CreateConsulMock(string serviceName)
        {
            var mock = Substitute.For<INodeMonitor>();
            _consulNodesResults[serviceName] = () => new INode[] {new Node(hostName: "dummy", version: ServiceVersion)};
            mock.Nodes.Returns(_=>_consulNodesResults[serviceName]());
            mock.WasUndeployed.Returns(_ => !_consulServiceList.Contains(serviceName));
            _consulNodeMonitors[serviceName] = mock;

            _consulServiceList = _consulServiceList.Add(serviceName);
            _serviceListVersion++;
        }

        [TearDown]
        public void TearDown()
        {
            _unitTestingKernel.Dispose();
            foreach (var consulClient in _consulNodeMonitors)
            {
                consulClient.Value.Dispose();
            }
        }

        [Test]
        [Repeat(Repeat)]
        public async Task QueryNotDefinedShouldFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);
            var nextHost = GetServiceDiscovery().GetNode();
            nextHost.Result.Hostname.ShouldBe(MasterService);
        }

        [Test]
        [Ignore("New ServiceDiscovery implementation does not support DataCenter scopes")]
        [Repeat(Repeat)]
        public async Task ScopeDataCenterShouldUseServiceNameAsConsoleQuery()
        {
            _configDic[$"Discovery.Services.{_serviceName}.Scope"] = "DataCenter";
            SetMockToReturnHost(_serviceName);
            var nextHost = GetServiceDiscovery().GetNode();
            (await nextHost).Hostname.ShouldBe(_serviceName);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenQueryDeleteShouldFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var discovey = GetServiceDiscovery();
            
            var node = discovey.GetNode();
            (await node).Hostname.ShouldBe(OriginatingService);

            SetMockToReturnServiceNotDefined(OriginatingService);
            

            node = discovey.GetNode();
            (await node).Hostname.ShouldBe(MasterService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task WhenServiceAddedShouldNotFallBackToMaster()
        {
            var reloadInterval = TimeSpan.FromMilliseconds(5);
            _configDic[$"Discovery.Services.{_serviceName}.ReloadInterval"] = reloadInterval.ToString();

            SetMockToReturnHost(MasterService);
            SetMockToReturnServiceNotDefined(OriginatingService);

            var discovey = GetServiceDiscovery();

            var node = discovey.GetNode();
            (await node).Hostname.ShouldBe(MasterService);

            SetMockToReturnHost(OriginatingService);
            
            node = discovey.GetNode();
            node.Result.Hostname.ShouldBe(OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public async Task ShouldNotFallBackToMasterOnConsulError()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnError(OriginatingService);
            Should.Throw<EnvironmentException>(() => GetServiceDiscovery().GetNode());
        }

        [Test]
        [Repeat(Repeat)]
        public async Task QueryDefinedShouldNotFallBackToMaster()
        {
            SetMockToReturnHost(MasterService);
            SetMockToReturnHost(OriginatingService);

            var nextHost = GetServiceDiscovery().GetNode();
            (await nextHost).Hostname.ShouldBe(OriginatingService);
        }

        [Test]
        [Repeat(Repeat)]
        public void MasterShouldNotFallBack()
        {
            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            _environmentVariableProvider.DataCenter.Returns("il3");
            _environmentVariableProvider.DeploymentEnvironment.Returns(MASTER_ENVIRONMENT);
            _unitTestingKernel.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

            SetMockToReturnServiceNotDefined(MasterService);

            Should.Throw<EnvironmentException>(() => GetServiceDiscovery().GetNode());
        }

        private void SetMockToReturnHost(string serviceName)
        {
            if (!_consulNodeMonitors.ContainsKey(serviceName))
                CreateConsulMock(serviceName);

            var newNodes = new INode[]{new Node(serviceName)};
            _consulNodesResults[serviceName] = () => newNodes;

            _consulServiceList = _consulServiceList.Add(serviceName);
            _serviceListVersion++;
        }

        private void SetMockToReturnServiceNotDefined(string serviceName)
        {
            _consulServiceList = _consulServiceList.Remove(serviceName);
            _serviceListVersion++;
        }

        private void SetMockToReturnError(string serviceName)
        {
            _consulNodesResults[serviceName] = () => throw new EnvironmentException("Mock: some error");
        }

        [Test]
        public void ServiceDiscoveySameNameShouldBeTheSame()
        {
            Assert.AreEqual(GetServiceDiscovery(), GetServiceDiscovery());
        }

        private readonly ReachabilityChecker _reachabilityChecker = x => Task.FromResult(true);

        private INewServiceDiscovery GetServiceDiscovery()
        {
            var discovery =
                _unitTestingKernel.Get<Func<string, ReachabilityChecker, INewServiceDiscovery>>()(_serviceName,
                    _reachabilityChecker);
            return discovery;
        }


        private string MasterService => ConsulServiceName(_serviceName, MASTER_ENVIRONMENT);
        private string OriginatingService => ConsulServiceName(_serviceName, ORIGINATING_ENVIRONMENT);

        private static string ConsulServiceName(string serviceName, string deploymentEnvironment) =>
            $"{serviceName}-{deploymentEnvironment}";
    }
}