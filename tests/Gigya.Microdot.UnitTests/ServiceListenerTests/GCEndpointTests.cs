using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using Gigya.Microdot.UnitTests.ServiceProxyTests;
using Metrics;
using Newtonsoft.Json;
using Ninject;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{
    [TestFixture, Parallelizable(ParallelScope.None)]
    public class GcEndpointTets:AbstractServiceProxyTest
    {
        private NonOrleansServiceTester<ConfigurableHost<IDemoService>> _testinghost;
        private SpyEventPublisher _flumeQueue;
        private LogSpy _logSpy;

        [SetUp]
        public void OneTimeSetUp()
        {
            _testinghost = new NonOrleansServiceTester<ConfigurableHost<IDemoService>>();
            _flumeQueue = _testinghost.Host.SpyEventPublisher;
            _logSpy = _testinghost.Host.LogSpy;
        }

        [TearDown]
        public virtual void TearDown()
        {
            try
            {
                _testinghost.Dispose();
            }
            catch
            {
                //should not fail tests
            }
        }

        [Test]
        public async Task TestRunGCWhenConfigEnabled()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");
            _testinghost.Host.MicrodotHostingConfigMock.GCEndpointEnabled = true;
            _testinghost.Host.LogSpy.ClearLog();
            

            var httpClient = new HttpClient();
            
            var uri = $"http://localhost:{_testinghost.BasePort}/force-traffic-affecting-gc?getToken=";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            var gcHandlingResult = JsonConvert.DeserializeObject<GCHandlingResult>(responseString);
            
            Assert.AreEqual("GC token generated", gcHandlingResult.Message);


            Assert.AreEqual(1, _logSpy.LogEntries.Count(), string.Join(Environment.NewLine, _logSpy.LogEntries.Select(x=>x.Message)) );
            var logEntry = _logSpy.LogEntries.Single();
            Assert.AreEqual("GC getToken was called, see result in Token tag", logEntry.Message);
            var tokenTag = logEntry.UnencryptedTags["tags.Token"].ToUpper();

            uri = $"http://localhost:{_testinghost.BasePort}/force-traffic-affecting-gc?gcType=Gen0&token={tokenTag.Replace("\"","")}";

            response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            responseString = await response.Content.ReadAsStringAsync();
            gcHandlingResult = JsonConvert.DeserializeObject<GCHandlingResult>(responseString);
            
            Assert.AreEqual("GC ran successfully", gcHandlingResult.Message);
            
            Assert.AreEqual(0, _flumeQueue.Events.Count);
        }
        
        [Test]
        public async Task TestCantRunGCWithoutValidToken()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");
            _testinghost.Host.MicrodotHostingConfigMock.GCEndpointEnabled = true;
            _testinghost.Host.LogSpy.ClearLog();
            

            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/force-traffic-affecting-gc?gcType=Gen0&token={Guid.NewGuid()}";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            var gcHandlingResult = JsonConvert.DeserializeObject<GCHandlingResult>(responseString);
            
            Assert.AreEqual("Illegal or missing token", gcHandlingResult.Message);
            
            Assert.AreEqual(0, _flumeQueue.Events.Count);
        }
        
        [Test]
        public async Task TestDontRunGCWhenConfigDisabled()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");
            _testinghost.Host.MicrodotHostingConfigMock.GCEndpointEnabled = false;

            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/force-traffic-affecting-gc?gcType=Gen0";

            var response = await httpClient.GetAsync(uri);

            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
