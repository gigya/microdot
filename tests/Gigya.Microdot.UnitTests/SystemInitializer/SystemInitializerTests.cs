using System;
using System.Net;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Testing.Shared;
using Ninject;
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
    }
}
