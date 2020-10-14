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
{/*
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class ClientCallEventTests
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

            _flumeQueue = (SpyEventPublisher) _serviceTester.CommunicationKernel.Get<IEventPublisher>();
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _serviceTester?.Dispose();
        }

        [Test]
        [Repeat(REPEAT)]
        public async Task SingleServerCall_CallSucceeds_PublishesCallEvent()
        {
            _flumeQueue.Clear();

            var requestId = nameof(SingleServerCall_CallSucceeds_PublishesCallEvent) + Guid.NewGuid();

            TracingContext.SetRequestID(requestId);
            TracingContext.TryGetRequestID();

            await _serviceProxy.Add(5, 3);
            await Task.Delay(100);
            var events = _flumeQueue.Events;
            var clientCallEvent = (ClientCallEvent ) events.Single();
            Assert.AreEqual("clientReq", clientCallEvent.EventType);
            Assert.AreEqual(nameof(CalculatorService), clientCallEvent.TargetService);
            Assert.AreEqual("Add", clientCallEvent.TargetMethod);
            Assert.AreEqual(requestId, clientCallEvent.RequestId);
        }

        
        [Test]
        [Repeat(REPEAT)]
        public async Task SingleServerCall_CallSucceeds_PublishesCallEvent_WithTags()
        {
            _flumeQueue.Clear();

            var requestId = nameof(SingleServerCall_CallSucceeds_PublishesCallEvent_WithTags) + Guid.NewGuid();

            TracingContext.SetRequestID(requestId);
            TracingContext.TryGetRequestID();

            using (var tag = TracingContext.Tags.TagUnencrypted("outsideOfScope", "IAmTag", true))
            {

            }


            using (var tag = TracingContext.Tags.TagUnencrypted("scopedTag", "IAmTag", true))
            {
                TracingContext.Tags.TagUnencrypted("int", 1, true);
                TracingContext.Tags.TagUnencrypted("encrypted", "IAmEncryptedTag", encryptedLog: true);
                await _serviceProxy.Add(5, 3);
            }

            await Task.Delay(100);


            var events = _flumeQueue.Events;
            var clientCallEvent = (ClientCallEvent) events.Single();

            Assert.Multiple(() =>
            {
                var tags = clientCallEvent.ContextTags.ToDictionary(x => x.Key, x => x.Value);
                var encryptedTags = clientCallEvent.ContextTagsEncrypted.ToDictionary(x => x.Key, x => x.Value);

                CollectionAssert.DoesNotContain(tags.Keys, "outsideOfScope");
                CollectionAssert.Contains(tags.Keys, "scopedTag");
                CollectionAssert.Contains(tags.Keys, "int");
                CollectionAssert.Contains(encryptedTags.Keys, "encrypted");

                Assert.AreEqual("IAmTag", tags["scopedTag"]);
                Assert.AreEqual(1, tags["int"]);
                Assert.AreEqual("IAmEncryptedTag", encryptedTags["encrypted"]);

            });

        }

    }*/

}