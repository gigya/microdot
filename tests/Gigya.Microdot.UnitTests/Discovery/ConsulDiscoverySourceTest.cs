using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Utils;
using Ninject;

using NSubstitute;

using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ConsulDiscoverySourceTest
    {
        private const string SERVICE_NAME = "ServiceName";
        private const string ENV = "env";

        private readonly string[] _endpointsBeforeChange = { "Endpoint1", "Endpoint2", "Endpoint3" };
        private readonly string[] _endpointsAfterChange = { "NewEndpoint1", "NewEndpoint2", "NewEndpoint3", "NewEndpoint4" };
        private TestingKernel<ConsoleLog> _unitTestingKernel;
        private IKernel Kernel => _unitTestingKernel;
        private ServiceDiscoverySourceBase _consulDiscoverySource;
        private IConsulClient _consulClientMock;
        private Func<EndPointsResult> _getConsulEndPoints;
        private BroadcastBlock<EndPointsResult> _resultChanged;

        private TaskCompletionSource<EndPointsResult> _consulResponseDelay;

        private ServiceScope _serviceScope;
        private string _requestedConsulServiceName;
        private TimeSpan _reloadInterval = TimeSpan.FromSeconds(1);
        private Dictionary<string, string> _configDic;
        private Func<Task> _consulClientInitTask;
        private DateTimeFake _dateTimeFake;
        private IEnvironment _environmentMock;

        [SetUp]
        public void Setup()
        {
            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k => {}, _configDic);

            _environmentMock = Substitute.For<IEnvironment>();
            _environmentMock.DeploymentEnvironment.Returns(ENV);
            Kernel.Rebind<IEnvironment>().ToConstant(_environmentMock);

            SetupDateTimeFake();
            SetupConsulClient();
        }

        private void SetupDateTimeFake()
        {
            _dateTimeFake = new DateTimeFake(manualDelay:true);
            Kernel.Rebind<IDateTime>().ToConstant(_dateTimeFake);
        }

        private void SetupConsulClient()
        {            
            SetConsulEndpoints(_endpointsBeforeChange);
            _resultChanged = new BroadcastBlock<EndPointsResult>(null);
            _consulClientMock = Substitute.For<IConsulClient>();
            _consulClientMock.Result.Returns(_ => _getConsulEndPoints());
            _consulClientMock.ResultChanged.Returns(_resultChanged);
            _consulClientMock.Init().Returns(_=>_consulClientInitTask());
            Kernel.Rebind<Func<string, IConsulClient>>().ToMethod(c=> s =>
            {
                _requestedConsulServiceName = s;
                return _consulClientMock;
            });
            _consulClientInitTask = ()=>Task.Run(()=>_resultChanged.SendAsync(_getConsulEndPoints()));
        }

        [TearDown]
        public void TearDown()
        {
            _consulDiscoverySource?.ShutDown();
            _unitTestingKernel.Dispose();
        }

        [Test]
        public async Task ReturnEmptyListIfConsulHasError()
        {
            ConsulError();
            await GetFirstResult().ConfigureAwait(false);
            AssertNoEndpoints();
        }

        [Test]
        public async Task ReturnEmptyListIfConsulNeverResponds()
        {
            ConsulNotResponds();

            var init = GetFirstResult().ConfigureAwait(false);
            await Task.Delay(50);
            _dateTimeFake.StopDelay(); // do not wait 10 seconds for timeout from Consul (in order to make test shorter)
            await init;

            AssertNoEndpoints();
        }

        [Test]
        public async Task ServiceInEnvironmentScope()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Scope"] = "Environment";            
            await GetFirstResult().ConfigureAwait(false);
            Assert.AreEqual($"{SERVICE_NAME}-{ENV}", _requestedConsulServiceName);
        }

        [Test]
        public async Task ServiceInZoneScope()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Scope"] = "Zone";
            await _unitTestingKernel.Get<ManualConfigurationEvents>().ApplyChanges<DiscoveryConfig>();

            await GetFirstResult().ConfigureAwait(false);
            Assert.AreEqual($"{SERVICE_NAME}", _requestedConsulServiceName);
        }

        private async Task GetFirstResult()
        {            
            var config = new ServiceDiscoveryConfig
            {
                Scope = _serviceScope,
            };
            var sourceFactory = Kernel.Get<Func<DeploymentIdentifier, ServiceDiscoveryConfig, ConsulDiscoverySource>>();
            var serviceContext = new DeploymentIdentifier(SERVICE_NAME, ENV, _environmentMock);
            _consulDiscoverySource = sourceFactory(serviceContext, config);
            await _consulDiscoverySource.Init();
            await GetNewResult();
        }

        private async Task GetNewResult()
        {
            var waitForChangeEvent = _resultChanged.WhenEventReceived();
            _resultChanged.Post(_getConsulEndPoints());
            await waitForChangeEvent;
        }

        private void SetConsulEndpoints(params string[] endpoints)
        {
            _getConsulEndPoints = () => new EndPointsResult { EndPoints = endpoints.Select(s => new ConsulEndPoint { HostName = s }).ToArray() };
        }

        private void ConsulEndpointsChanged()
        {
            SetConsulEndpoints(_endpointsAfterChange);
        }

        private void ConsulNotResponds()
        {
            _consulClientInitTask = ()=>new TaskCompletionSource<bool>().Task; // task which never ends
            _getConsulEndPoints = () => null;
        }

        private void ConsulError()
        {
            _getConsulEndPoints = () => new EndPointsResult { Error = new Exception("Consul not responding") };
        }

        private void AssertNoEndpoints()
        {
            _consulDiscoverySource.IsServiceDeploymentDefined.ShouldBe(true);
            Assert.AreEqual(0, _consulDiscoverySource.Result.EndPoints.Length, "Endpoints list should be empty");
        }

    }

}