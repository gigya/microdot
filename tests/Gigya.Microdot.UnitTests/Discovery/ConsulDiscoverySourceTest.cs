using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.Testing;

using Ninject;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

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
        private DateTimeFake _dateTimeFake;
        private IConsulClient _consulClientMock;
        private Func<Task<EndPointsResult>> _getConsulEndPointsTask;

        private TaskCompletionSource<EndPointsResult> _consulResponseDelay;
        private Task<EndPointsResult> _consulTask;

        private ServiceScope _serviceScope;
        private string _requestedConsulServiceName;
        private TimeSpan _reloadInterval = TimeSpan.FromSeconds(1);

        [SetUp]
        public void Setup()
        {
            _unitTestingKernel = new TestingKernel<ConsoleLog>();

            _dateTimeFake = new DateTimeFake { UtcNow = new DateTime(2016, 11, 11) };
            Kernel.Rebind<IDateTime>().ToConstant(_dateTimeFake);

            var environmentVarialbesMock = Substitute.For<IEnvironmentVariableProvider>();
            environmentVarialbesMock.DeploymentEnvironment.Returns(ENV);
            Kernel.Rebind<IEnvironmentVariableProvider>().ToConstant(environmentVarialbesMock);

            SetupConsulClientAdapter();
        }

        private void SetupConsulClientAdapter()
        {
            _consulTask = null;
            SetConsulEndpoints(_endpointsBeforeChange);
            _consulClientMock = Substitute.For<IConsulClient>();
            _consulClientMock.GetEndPoints(Arg.Any<string>()).Returns(_ =>
            {
                _requestedConsulServiceName = (string)_.Args()[0];
                return _consulTask = _getConsulEndPointsTask();
            });
            Kernel.Rebind<IConsulClient>().ToConstant(_consulClientMock);
        }

        [TearDown]
        public void TearDown()
        {
            _consulDiscoverySource?.ShutDown();
            _unitTestingKernel.Dispose();
        }

        [Test]
        public async Task ReloadEndpointsAfterConfiguredInterval()
        {
            TimeSpan expectedInterval = TimeSpan.FromMilliseconds(1234);
            _reloadInterval = expectedInterval;
            await GetFirstResult().ConfigureAwait(false);
            await Task.Delay(1500);

            _dateTimeFake.DelaysRequested.Single().ShouldBe(expectedInterval);
        }

        [Test]
        public async Task ReturnEmptyListIfConsulNeverResponded()
        {
            ConsulNotResponding();
            await GetFirstResult().ConfigureAwait(false);
            AssertNoEndpoints();
        }

        [Test]
        public async Task ReturnLastResultUntilConsulResponds()
        {
            await GetFirstResult().ConfigureAwait(false);
            ConsulResponseIsDelayed();
            OneSecondPassed();

            await GetNewResult().ConfigureAwait(false);
            AssertEndpointsNotChanged();

            ConsulResponseArrived();
            await Task.Delay(100);
            await GetNewResult().ConfigureAwait(false);
            AssertEndpointsChanged();
        }

        [Test]
        public async Task ServiceInEnvironmentScope()
        {
            _serviceScope = ServiceScope.Environment;
            await GetFirstResult().ConfigureAwait(false);
            Assert.AreEqual($"{SERVICE_NAME}-{ENV}", _requestedConsulServiceName);
        }

        [Test]
        public async Task ServiceInDataCenterScope()
        {
            _serviceScope = ServiceScope.DataCenter;
            await GetFirstResult().ConfigureAwait(false);
            Assert.AreEqual($"{SERVICE_NAME}", _requestedConsulServiceName);
        }

        private async Task GetFirstResult()
        {
            var config = new ServiceDiscoveryConfig
            {
                Scope = _serviceScope,
                ReloadInterval = _reloadInterval
            };
            var sourceFactory = Kernel.Get<Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulDiscoverySource>>();
            var serviceContext = new ServiceDeployment(SERVICE_NAME, ENV);
            _consulDiscoverySource = sourceFactory(serviceContext, config);
            await WaitUntilConsulRespondsOrTimeout().ConfigureAwait(false);
        }

        private async Task GetNewResult()
        {
            await WaitUntilConsulRespondsOrTimeout().ConfigureAwait(false);
        }

        private async Task WaitUntilConsulRespondsOrTimeout()
        {
            while (_consulTask == null)
                await Task.Delay(100).ConfigureAwait(false);
            await Task.WhenAny(Task.Delay(100), _consulTask).ConfigureAwait(false);
        }

        private void SetConsulEndpoints(params string[] endpoints)
        {
            _getConsulEndPointsTask = () => Task.FromResult(new EndPointsResult { EndPoints = endpoints.Select(s => new ConsulEndPoint { HostName = s }).ToArray() });
        }

        private void ConsulEndpointsChanged()
        {
            SetConsulEndpoints(_endpointsAfterChange);
        }

        private void ConsulNotResponding()
        {
            _getConsulEndPointsTask = () => Task.FromResult(new EndPointsResult { Error = new Exception("Consul not responding") });
        }

        private void ConsulResponseIsDelayed()
        {
            _consulResponseDelay = new TaskCompletionSource<EndPointsResult>();
            _getConsulEndPointsTask = () => _consulResponseDelay.Task;
        }

        private void ConsulResponseArrived()
        {
            ConsulEndpointsChanged();
            _consulResponseDelay.SetResult(_getConsulEndPointsTask().Result);
        }

        private void AssertEndpointsNotChanged()
        {
            CollectionAssert.AreEqual(_endpointsBeforeChange, _consulDiscoverySource.EndPoints.EndPoints.Select(_ => _.HostName).ToArray(), "Endpoints list was changed");
        }

        private void AssertEndpointsChanged()
        {
            CollectionAssert.AreEqual(_endpointsAfterChange, _consulDiscoverySource.EndPoints.EndPoints.Select(_ => _.HostName), "Endpoints list did not update");
        }

        private void AssertNoEndpoints()
        {
            Assert.AreEqual(0, _consulDiscoverySource.EndPoints.EndPoints.Length, "Endpoints list should be empty");
        }

        private void OneSecondPassed()
        {
            _dateTimeFake.StopDelay();
        }
    }

    public class SimpleTargetBlock<T> : ITargetBlock<T>
    {
        private readonly Action<T> _doWhenMessageOffered;

        public SimpleTargetBlock(Action<T> doWhenMessageOffered = null)
        {
            _doWhenMessageOffered = doWhenMessageOffered;
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue, ISourceBlock<T> source, bool consumeToAccept)
        {
            _doWhenMessageOffered?.Invoke(messageValue);
            return DataflowMessageStatus.Accepted;
        }

        public void Complete()
        {
        }

        public void Fault(Exception exception)
        {
        }

        public Task Completion => Task.FromResult(true);
    }
}