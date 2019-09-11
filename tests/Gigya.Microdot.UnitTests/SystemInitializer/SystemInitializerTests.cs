using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.SystemInitializer
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
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

        [Test]
        public void ShouldNotRebindIConfigObjectDuringInitializingIfAlreadyBindedBefore()
        {
            DataCentersConfig config = new DataCentersConfig();
            config.Current = "Test";

            BroadcastBlock<DataCentersConfig> block = new BroadcastBlock<DataCentersConfig>(null);

            TestingKernel<ConsoleLog> kernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<DataCentersConfig>().ToConstant(config);
                k.Rebind<Func<DataCentersConfig>>().ToMethod(c => () => config);
                k.Rebind<ISourceBlock<DataCentersConfig>>().ToConstant(block);
                k.Rebind<Func<ISourceBlock<DataCentersConfig>>>().ToMethod(c => () => block);
            });

            Assert.AreSame(config, kernel.Get<DataCentersConfig>());
            Assert.AreSame(config, kernel.Get<Func<DataCentersConfig>>()());
            Assert.AreSame(block, kernel.Get<ISourceBlock<DataCentersConfig>>());
            Assert.AreSame(block, kernel.Get<Func<ISourceBlock<DataCentersConfig>>>()());
        }
    }
}
