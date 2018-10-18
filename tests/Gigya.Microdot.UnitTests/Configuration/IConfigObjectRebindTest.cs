using System;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Ninject;
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
        private IConfigObjectCreator _configWrapperMock = Substitute.For<IConfigObjectCreator>();

        [SetUp]
        public void SetUp()
        {
            _testingKernel = new StandardKernel();
            _testingKernel.Rebind<Func<Type, IConfigObjectCreator>>().ToMethod(t => tp => _configWrapperMock);
            _testingKernel.Load<MicrodotModule>();
        }

        [TearDown]
        public void TearDown()
        {
            _testingKernel.Dispose();
            _configWrapperMock.ClearReceivedCalls();
        }

        [Test]
        public void ShouldCallGetLatestWhileResolvingObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _testingKernel.Get<DiscoveryConfig>();

            _configWrapperMock.Received(1).GetLatest();
            object notes = _configWrapperMock.DidNotReceive().ChangeNotifications;
        }

        [Test]
        public void ShouldCallGetLatestWhileResolvingFuncObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _testingKernel.Get<Func<DiscoveryConfig>>()();

            _configWrapperMock.Received(1).GetLatest();
            object notes = _configWrapperMock.DidNotReceive().ChangeNotifications;
        }

        [Test]
        public void ShouldCallChangeNotificationsWhileResolvingISourceBlockObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _configWrapperMock.ChangeNotifications.Returns(Substitute.For<ISourceBlock<DiscoveryConfig>>());
            _testingKernel.Get<ISourceBlock<DiscoveryConfig>>();

            _configWrapperMock.DidNotReceive().GetLatest();
            object notifications = _configWrapperMock.Received(1).ChangeNotifications;
        }

        [Test]
        public void ShouldCallChangeNotificationsWhileResolvingFuncISourceBlockObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _configWrapperMock.ChangeNotifications.Returns(Substitute.For<ISourceBlock<DiscoveryConfig>>());
            _testingKernel.Get<Func<ISourceBlock<DiscoveryConfig>>>()();

            object notifications = _configWrapperMock.Received(1).ChangeNotifications;
            _configWrapperMock.DidNotReceive().GetLatest();
        }
    }
}
