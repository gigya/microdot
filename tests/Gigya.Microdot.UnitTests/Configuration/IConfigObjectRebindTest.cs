using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.Testing.Shared;
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
        public void RebindObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _testingKernel.Get<DiscoveryConfig>();

            _configWrapperMock.Received(1).GetLatest();
            object notes = _configWrapperMock.DidNotReceive().ChangeNotifications;
        }

        [Test]
        public void RebindFuncObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _testingKernel.Get<Func<DiscoveryConfig>>()();

            _configWrapperMock.Received(1).GetLatest();
            object notes = _configWrapperMock.DidNotReceive().ChangeNotifications;
        }

        [Test]
        public void RebindISourceObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _configWrapperMock.ChangeNotifications.Returns(Substitute.For<ISourceBlock<DiscoveryConfig>>());
            _testingKernel.Get<ISourceBlock<DiscoveryConfig>>();

            object notes = _configWrapperMock.Received(1).ChangeNotifications;
            _configWrapperMock.DidNotReceive().GetLatest();
        }

        [Test]
        public void RebindFuncISourceObject()
        {
            _configWrapperMock.GetLatest().Returns(new DiscoveryConfig());
            _configWrapperMock.ChangeNotifications.Returns(Substitute.For<ISourceBlock<DiscoveryConfig>>());
            _testingKernel.Get<Func<ISourceBlock<DiscoveryConfig>>>()();

            object notes = _configWrapperMock.Received(1).ChangeNotifications;
            _configWrapperMock.DidNotReceive().GetLatest();
        }
    }
}
