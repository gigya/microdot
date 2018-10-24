using System;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.ServiceDiscovery.Config;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.Configuration
{
    [TestFixture]
    public class IConfigObjectRebindTest
    {
        private StandardKernel _testingKernel;
        private IConfigObjectCreator _configObjectCreatorMock = Substitute.For<IConfigObjectCreator>();

        [SetUp]
        public void SetUp()
        {
            _testingKernel = new StandardKernel();
            _testingKernel.Rebind<Func<Type, IConfigObjectCreator>>().ToMethod(t => tp => _configObjectCreatorMock);
            _testingKernel.Load<MicrodotModule>();
            _testingKernel.Rebind<SystemInitializerBase>().To<SystemInitializerFake>();

            ILog logFake = Substitute.For<ILog>();
            _testingKernel.Rebind<ILog>().ToConstant(logFake);

            _testingKernel.Get<SystemInitializerBase>().Init();
        }

        [TearDown]
        public void TearDown()
        {
            _testingKernel.Dispose();
            _configObjectCreatorMock.ClearReceivedCalls();
        }

        [Test]
        public void ShouldCallGetLatestWhileResolvingObject()
        {
            _configObjectCreatorMock.GetLatest().Returns(new DiscoveryConfig());
            _testingKernel.Get<DiscoveryConfig>();

            _configObjectCreatorMock.Received(1).GetLatest();
            object notes = _configObjectCreatorMock.DidNotReceive().ChangeNotifications;
        }

        [Test]
        public void ShouldCallChangeNotificationsWhileResolvingISourceBlockObject()
        {
            _configObjectCreatorMock.GetLatest().Returns(new DiscoveryConfig());
            _configObjectCreatorMock.ChangeNotifications.Returns(Substitute.For<ISourceBlock<DiscoveryConfig>>());
            _testingKernel.Get<ISourceBlock<DiscoveryConfig>>();

            _configObjectCreatorMock.DidNotReceive().GetLatest();
            object notifications = _configObjectCreatorMock.Received(1).ChangeNotifications;
        }

        class SystemInitializerFake : SystemInitializerBase
        {
            public SystemInitializerFake(IKernel kernel) : base(kernel)
            {
            }

            protected override void SetDefaultTCPHTTPSettings()
            { }

            protected override void InitWorkloadMetrics()
            {
            }

            public override void Dispose()
            {
            }
        }
    }
}
