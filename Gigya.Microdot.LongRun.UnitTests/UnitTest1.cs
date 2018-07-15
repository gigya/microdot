//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Threading.Tasks.Dataflow;
//using Gigya.Common.Contracts.Exceptions;
//using Gigya.Microdot.Fakes;
//using Gigya.Microdot.Interfaces.Configuration;
//using Gigya.Microdot.Interfaces.SystemWrappers;
//using Gigya.Microdot.ServiceDiscovery;
//using Gigya.Microdot.Testing;
//using Gigya.Microdot.Testing.Shared;
//using Gigya.Microdot.Testing.Shared.Utils;
//using Gigya.Microdot.UnitTests.Discovery;
//using Metrics;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Ninject;
//using NSubstitute;
//using NUnit.Framework;
//using Shouldly;
//using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
//using Timer = System.Threading.Timer;

//namespace Gigya.Microdot.LongRun.UnitTests
//{
//    [TestClass]
//    public class UnitTest1
//    {

//        private const string ServiceVersion = "1.2.30.1234";
//        private string _serviceName;
//        private const string MASTER_ENVIRONMENT = "prod";
//        private const string ORIGINATING_ENVIRONMENT = "fake_env";
//        private readonly TimeSpan _timeOut = TimeSpan.FromSeconds(5);
//        private Dictionary<string, string> _configDic;
//        private TestingKernel<ConsoleLog> _unitTestingKernel;
//        private Dictionary<string, ConsulClientMock> _consulClient;
//        private IEnvironmentVariableProvider _environmentVariableProvider;
//        private ManualConfigurationEvents _configRefresh;

//        private IDateTime _dateTimeMock;
//        private int id;
//        private const int Repeat = 1;


//        [SetUp]
//        public void SetUp()
//        {
//            _unitTestingKernel?.Dispose();
//            _serviceName = $"ServiceName{++id}";

//            _environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
//            _environmentVariableProvider.DataCenter.Returns("il3");
//            _environmentVariableProvider.DeploymentEnvironment.Returns(ORIGINATING_ENVIRONMENT);

//            _configDic = new Dictionary<string, string> { { "Discovery.EnvironmentFallbackEnabled", "true" } };
//            _unitTestingKernel = new TestingKernel<ConsoleLog>(k =>
//            {
//                k.Rebind<IEnvironmentVariableProvider>().ToConstant(_environmentVariableProvider);

//                k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
//                SetupConsulClientMocks();
//                k.Rebind<Func<string, IConsulClient>>().ToMethod(_ => (s => _consulClient[s]));

//                _dateTimeMock = Substitute.For<IDateTime>();
//                _dateTimeMock.Delay(Arg.Any<TimeSpan>()).Returns(c => Task.Delay(TimeSpan.FromMilliseconds(100)));
//                k.Rebind<IDateTime>().ToConstant(_dateTimeMock);
//            }, _configDic);
//            _configRefresh = _unitTestingKernel.Get<ManualConfigurationEvents>();

//            var environmentVariableProvider = _unitTestingKernel.Get<IEnvironmentVariableProvider>();
//            Assert.AreEqual(_environmentVariableProvider, environmentVariableProvider);
//        }

//        private static string ConsulServiceName(string serviceName, string deploymentEnvironment) => $"{serviceName}-{deploymentEnvironment}";
//        private string MasterService => ConsulServiceName(_serviceName, MASTER_ENVIRONMENT);
//        private string OriginatingService => ConsulServiceName(_serviceName, ORIGINATING_ENVIRONMENT);


//        private void SetupConsulClientMocks()
//        {
//            _consulClient = new Dictionary<string, ConsulClientMock>();

//            CreateConsulMock(MasterService);
//            CreateConsulMock(OriginatingService);

//        }

//        private void CreateConsulMock(string serviceName)
//        {
//            var mock = new ConsulClientMock();
//            mock.SetResult(new EndPointsResult
//            {
//                EndPoints = new EndPoint[] { new ConsulEndPoint { HostName = "dumy", Version = ServiceVersion } },
//                IsQueryDefined = true
//            });

//            _consulClient[serviceName] = mock;
//        }

//        [TestMethod]
//        public void TestMethod1()
//        {
//        }
//    }
//}
