using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint;
using Gigya.Microdot.Hosting.Service;
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
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{
    [TestFixture, Parallelizable(ParallelScope.None)]
    public class GcEndpointTets:AbstractServiceProxyTest
    {
        private NonOrleansServiceTester<ConfigurableHost<IDemoService>> _testinghost;

        [SetUp]
        public virtual void SetUp()
        {
            _testinghost = new NonOrleansServiceTester<ConfigurableHost<IDemoService>>();
            TracingContext.SetRequestID("1");
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

            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/force-traffic-affecting-gc?gcType=Gen0";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            var gcHandlingResult = JsonConvert.DeserializeObject<GCHandlingResult>(responseString);
            
            Assert.AreEqual("GC ran successfully", gcHandlingResult.Message);
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
