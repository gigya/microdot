using System;
using System.Linq;
using Gigya.Microdot.SharedLogic.Events;
using NUnit.Framework;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Testing.Shared.Service;
using Ninject;

namespace Gigya.Common.OrleansInfra.FunctionalTests.Events
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class ServerCallEventTests
    {
        private const int REPEAT = 1;

        private SpyEventPublisher _flumeQueue;
        private ICalculatorService _serviceProxy;
        private NonOrleansServiceTester<CalculatorServiceHost> _serviceTester;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _serviceTester = new NonOrleansServiceTester<CalculatorServiceHost>();

            _serviceProxy = _serviceTester.GetServiceProxy<ICalculatorService>();
            
            _flumeQueue = (SpyEventPublisher)_serviceTester.Host.Kernel.Get<IEventPublisher>();
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _serviceTester?.Dispose();
        }

        [Test]
        [Repeat(REPEAT)]
        public async Task SingleServerCall_CallSucceeds_PublishesEvent()
        {
            _flumeQueue.Clear();

            var requestId = nameof(SingleServerCall_CallSucceeds_PublishesEvent) + Guid.NewGuid();
            
            TracingContext.SetRequestID(requestId);
            TracingContext.Tags().Tag("string", "IAmTag", true);
            TracingContext.Tags().Tag("int", 1, true);
            TracingContext.Tags().Tag("encrypted", "IAmEncryptedTag", encryptedLog: true);
            
            await _serviceProxy.Add(5, 3);
            await Task.Delay(100);
            var events = _flumeQueue.Events;
            var serverReq = (ServiceCallEvent) events.Single(); 
            
            Assert.AreEqual("IAmTag", serverReq.ContextTags.FirstOrDefault(e=> e.Key== "string").Value);
            Assert.AreEqual(1, serverReq.ContextTags.FirstOrDefault(e=> e.Key== "int").Value);
            Assert.AreEqual("IAmEncryptedTag", serverReq.EncryptedContextTags.FirstOrDefault(e=> e.Key== "encrypted").Value);
        }
         
    }
}