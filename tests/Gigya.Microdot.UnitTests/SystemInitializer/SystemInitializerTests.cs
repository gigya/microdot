using System;
using System.Linq;
using System.Net;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.SystemInitializer
{
    [TestFixture]
    public class SystemInitializerTests
    {
        [Test]
        public void ServicePointManagerIsUpdated()
        {
            TestingKernel<ConsoleLog> kernel = new TestingKernel<ConsoleLog>();

            ServicePointManagerDefaultConfig config = kernel.Get<Func<ServicePointManagerDefaultConfig>>()();

            Assert.AreEqual(ServicePointManager.DefaultConnectionLimit, config.DefaultConnectionLimit);
            Assert.AreEqual(ServicePointManager.UseNagleAlgorithm, config.UseNagleAlgorithm);
            Assert.AreEqual(ServicePointManager.Expect100Continue, config.Expect100Continue);
        }

        [Test]
        public void ShouldCallConfigObjectCacheWhileScanningAssembliesForIConfigObjects()
        {
            IConfigObjectsCache configCacheMock = Substitute.For<IConfigObjectsCache>();
            TestingKernel<ConsoleLog> kernel = new TestingKernel<ConsoleLog>(k => k.Rebind<IConfigObjectsCache>().ToConstant(configCacheMock));
            IAssemblyProvider aProvider = kernel.Get<IAssemblyProvider>();
            int typesCount = aProvider.GetAllTypes().Where(ConfigObjectCreator.IsConfigObject).Count();

            configCacheMock.Received(typesCount).RegisterConfigObjectCreator(Arg.Any<IConfigObjectCreator>());
        }
    }
}
