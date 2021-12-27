using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice
{
    [TestFixture]
    [NonParallelizable]
    public class GCEndpointTests
    {
        private IGCEndpointHandler _gcEndpointHandler;
        private IGCCollectionRunner _gcCollectionrSub;
        private ILog _logger;
        private IDateTime _dateTime;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _gcEndpointHandler = Substitute.For<IGCEndpointHandler>();
            _gcCollectionrSub = Substitute.For<IGCCollectionRunner>();
            _logger = Substitute.For<ILog>();
            _dateTime = Substitute.For<IDateTime>();
        }
        
        [SetUp]
        public void Setup()
        {
            _gcEndpointHandler.ClearReceivedCalls();
            _gcCollectionrSub.ClearReceivedCalls();
            _logger.ClearReceivedCalls();
            _dateTime.ClearReceivedCalls();
        }
        
        [Test]
        public async Task HandledGcEndpointTest()
        {
            var gcCollectionResult = new GCCollectionResult(100, 10, 55);
            var gcHandlingResult = 
                new GCHandlingResult(true, "foo bar", gcCollectionResult);
            
            _gcEndpointHandler
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>())
                .Returns(Task.FromResult(gcHandlingResult));
            
            var responses = new List<(string, HttpStatusCode, string)>();
            
            var gcEndpoint = new GCCustomEndpoint(_gcEndpointHandler);
            var tryHandleResult = await gcEndpoint.TryHandle(null, async (data, status, type) => responses.Add((data, status, type)));

            var receivedCalls = _gcEndpointHandler
                .Received(1)
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>());
            
            Assert.AreEqual(true, tryHandleResult);
            Assert.AreEqual(1, responses.Count());
            
            var deserializedResult = JsonConvert.DeserializeObject<GCHandlingResult>(responses.First().Item1);
            Assert.AreEqual(gcHandlingResult.Message, deserializedResult.Message);
            Assert.AreEqual(gcHandlingResult.GcCollectionResult.TotalMemoryAfterGC, deserializedResult.GcCollectionResult.TotalMemoryAfterGC);
            Assert.AreEqual(gcHandlingResult.GcCollectionResult.TotalMemoryBeforeGC, deserializedResult.GcCollectionResult.TotalMemoryBeforeGC);
        }
        
        [Test]
        public async Task NotHandledGcEndpointTest()
        {
            _gcEndpointHandler
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>())
                .Returns(Task.FromResult(new GCHandlingResult(false)));
            
            var responses = new List<(string, HttpStatusCode, string)>();
            
            var gcEndpoint = new GCCustomEndpoint(_gcEndpointHandler);
            var tryHandleResult = await gcEndpoint.TryHandle(null, async (data, status, type) => responses.Add((data, status, type)));

            var receivedCalls = _gcEndpointHandler
                .Received(1)
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>());
            
            Assert.AreEqual(false, tryHandleResult);
            Assert.AreEqual(0, responses.Count());
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_Off_Matching_Path()
        {
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = false
            }, _logger, _gcCollectionrSub, _dateTime);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", "gen0" }
                });
            
            Assert.IsFalse(gcHandlingResult.Successful);
            Assert.IsNull(gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_Not_Matching_Path()
        {
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            }, 
                _logger, _gcCollectionrSub, _dateTime);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/not-matching-path"), new NameValueCollection()
            {
                { "gcType", "someGcType" }
            });
            
            Assert.IsFalse(gcHandlingResult.Successful);
            Assert.IsNull(gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_Matching_Path_No_GcType()
        {
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            }, _logger, _gcCollectionrSub, _dateTime);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "wrongTypeParam", "gen0" }
                });
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual("GCEndpoint called with unsupported GCType",gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_Matching_Path_Wrong_GcType()
        {
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            },  _logger, _gcCollectionrSub, _dateTime);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", "gen99" }
                });
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual("GCEndpoint called with unsupported GCType",gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        [TestCase("Gen0")]
        [TestCase("Gen1")]
        [TestCase("Gen2")]
        [TestCase("LOHCompaction")]
        [TestCase("BlockingLohCompaction")]
        public async Task GCEndpointHandlerTest_On_Matching_Path_Right_GcType(string genType)
        {
            var totalMemoryBeforeGc = 500;
            var totalMemoryAfterGc = 70;
            var elapsedMilliseconds = 33;
            _gcCollectionrSub.Collect(Arg.Any<GCType>()).Returns(
                new GCCollectionResult(
                    totalMemoryBeforeGc, 
                    totalMemoryAfterGc, 
                    elapsedMilliseconds)
                );
            
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true,
            }, _logger, _gcCollectionrSub, _dateTime);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", genType }
                });
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual("GC ran successfully",gcHandlingResult.Message);
            Assert.NotNull(gcHandlingResult.GcCollectionResult);
            Assert.AreEqual(totalMemoryBeforeGc, gcHandlingResult.GcCollectionResult.TotalMemoryBeforeGC);
            Assert.AreEqual(totalMemoryAfterGc, gcHandlingResult.GcCollectionResult.TotalMemoryAfterGC);
            Assert.AreEqual(elapsedMilliseconds, gcHandlingResult.GcCollectionResult.ElapsedMilliseconds);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Dont_Collect_If_Cooldown_Required_Default_Value()
        {
            var totalMemoryBeforeGc = 500;
            var totalMemoryAfterGc = 70;
            var elapsedMilliseconds = 33;
            var gcEndpointCooldown = TimeSpan.FromHours(3);

            
            _gcCollectionrSub.Collect(Arg.Any<GCType>()).Returns(
                new GCCollectionResult(
                    totalMemoryBeforeGc, 
                    totalMemoryAfterGc, 
                    elapsedMilliseconds)
            );
            
            var firstTime = new DateTime(1983, 12, 3, 12, 0, 0);

            _dateTime.UtcNow.Returns(firstTime);
            
            var gcEndpointHandler = new GCEndpointHandler(() =>
            {
                return new MicrodotHostingConfig()
                {
                    GCEndpointEnabled = true,
                    GcEndpointCooldown = gcEndpointCooldown
                };
            }, _logger, _gcCollectionrSub, _dateTime);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", "Gen0" }
                });
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual("GC ran successfully",gcHandlingResult.Message);
            Assert.AreEqual(totalMemoryBeforeGc, gcHandlingResult.GcCollectionResult.TotalMemoryBeforeGC);
            Assert.AreEqual(totalMemoryAfterGc, gcHandlingResult.GcCollectionResult.TotalMemoryAfterGC);
            Assert.AreEqual(elapsedMilliseconds, gcHandlingResult.GcCollectionResult.ElapsedMilliseconds);
            
            gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", "Gen0" }
                });
            
            _gcCollectionrSub.Received(1).Collect(Arg.Any<GCType>());
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual($"GC call cooldown in effect, will be ready in {gcEndpointCooldown}",gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
            
            _dateTime.UtcNow.Returns(firstTime.Add(gcEndpointCooldown).AddSeconds(1));
            
            gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", "Gen1" }
                });
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual("GC ran successfully",gcHandlingResult.Message);
            Assert.AreEqual(totalMemoryBeforeGc, gcHandlingResult.GcCollectionResult.TotalMemoryBeforeGC);
            Assert.AreEqual(totalMemoryAfterGc, gcHandlingResult.GcCollectionResult.TotalMemoryAfterGC);
            Assert.AreEqual(elapsedMilliseconds, gcHandlingResult.GcCollectionResult.ElapsedMilliseconds);
        }
        
        [Test]
        [TestCase(GCType.Gen0)]
        [TestCase(GCType.Gen1)]
        [TestCase(GCType.Gen2)]
        [TestCase(GCType.LOHCompaction)]
        [TestCase(GCType.BlockingLohCompaction)]
        public async Task GCCollectionRunner_Sanity(GCType genType)
        {
            var gcCollectionRunner = new GCCollectionRunner();
            Assert.DoesNotThrow(()=>gcCollectionRunner.Collect(genType));
        }
    }
}