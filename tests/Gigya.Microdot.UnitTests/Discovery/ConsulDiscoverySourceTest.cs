using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Utils;
using Ninject;

using NSubstitute;

using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.Discovery
{
    [TestFixture]
    public class ConsulDiscoverySourceTest
    {
        private const string SERVICE_NAME = "ServiceName";
        private const string ENV = "DeploymentEnvironment";

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

        [SetUp]
        public void Setup()
        {
            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(k=>k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>(), _configDic);

            var environmentVarialbesMock = Substitute.For<IEnvironmentVariableProvider>();
            environmentVarialbesMock.DeploymentEnvironment.Returns(ENV);
            Kernel.Rebind<IEnvironmentVariableProvider>().ToConstant(environmentVarialbesMock);

            SetupConsulClient();
        }

        private void SetupConsulClient()
        {
            SetConsulEndpoints(_endpointsBeforeChange);
            _resultChanged = new BroadcastBlock<EndPointsResult>(null);
            _consulClientMock = Substitute.For<IConsulClient>();
            _consulClientMock.Result.Returns(_ => _getConsulEndPoints());
            _consulClientMock.ResultChanged.Returns(_resultChanged);
            Kernel.Rebind<Func<string, IConsulClient>>().ToMethod(c=> s =>
            {
                _requestedConsulServiceName = s;
                return _consulClientMock;
            });
        }

        [TearDown]
        public void TearDown()
        {
            _consulDiscoverySource?.ShutDown();
            _unitTestingKernel.Dispose();
        }

        [Test]
        public async Task ReturnEmptyListIfConsulNeverResponded()
        {
            ConsulNotResponding();
            await GetFirstResult().ConfigureAwait(false);
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
        public async Task ServiceInDataCenterScope()
        {
            _configDic[$"Discovery.Services.{SERVICE_NAME}.Scope"] = "DataCenter";            
            await GetFirstResult().ConfigureAwait(false);
            Assert.AreEqual($"{SERVICE_NAME}", _requestedConsulServiceName);
        }

        private async Task GetFirstResult()
        {            
            var config = new ServiceDiscoveryConfig
            {
                Scope = _serviceScope,
            };
            var sourceFactory = Kernel.Get<Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulDiscoverySource>>();
            var serviceContext = new ServiceDeployment(SERVICE_NAME, ENV);
            _consulDiscoverySource = sourceFactory(serviceContext, config);
            _consulDiscoverySource.Init();
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

        private void ConsulNotResponding()
        {
            _getConsulEndPoints = () => new EndPointsResult { Error = new Exception("Consul not responding") };
        }

        private void AssertNoEndpoints()
        {
            Assert.AreEqual(0, _consulDiscoverySource.Result.EndPoints.Length, "Endpoints list should be empty");
        }

    }

}