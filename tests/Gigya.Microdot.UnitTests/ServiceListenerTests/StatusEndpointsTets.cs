using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
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
    public class ConfigurableHost<T>:TestingHost<T> where T:class
    {
        public MicrodotHostingConfig MicrodotHostingConfigMock = new MicrodotHostingConfig();
        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            base.Configure(kernel,commonConfig);
            kernel.Rebind<Func<MicrodotHostingConfig>>().ToMethod(_ => ()=> MicrodotHostingConfigMock);
            
        }
    }
 
    [TestFixture, Parallelizable(ParallelScope.None)]
    public class StatusEndpointsTets:AbstractServiceProxyTest
    {
        private NonOrleansServiceTester<ConfigurableHost<IDemoService>> _testinghost;

        [SetUp]
        public virtual void SetUp()
        {
            _testinghost = new NonOrleansServiceTester<ConfigurableHost<IDemoService>>();

        //    Metric.Context("Service");
            TracingContext.SetRequestID("1");
        }
        
        [TearDown]
        public virtual void TearDown()
        {
            try
            {
                _testinghost.Dispose();
             //   Metric.ShutdownContext("Service");
            }
            catch
            {
                //should not fail tests
            }
        }

        [Test]
        public async Task TestGetStatus()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");

            _testinghost.Host.MicrodotHostingConfigMock.StatusEndpoints = 
                new List<string>(new []{"/myStatus"});
            _testinghost.Host.MicrodotHostingConfigMock.ShouldLogStatusEndpoint = true;
            
            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/myStatus";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
        
        [Test]
        public async Task TestGetStatusShouldNotWorkIfEndpointDontMatch()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");

            _testinghost.Host.MicrodotHostingConfigMock.StatusEndpoints = 
                new List<string>(new []{"/status"});
            _testinghost.Host.MicrodotHostingConfigMock.ShouldLogStatusEndpoint = false;
            
            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/myStatus";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
        
        [Test]
        public async Task TestGetStatusWorkWithMultipleConfigValues()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");

            _testinghost.Host.MicrodotHostingConfigMock.StatusEndpoints = 
                new List<string>(new []{"/status", "/myStatus", "/someStatus"});
            
            _testinghost.Host.MicrodotHostingConfigMock.ShouldLogStatusEndpoint = false;
            
            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/myStatus";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
        
        [Test]
        public async Task TestGetStatusShouldNotWorkForSuffix()
        {
            var client = _testinghost.GetServiceProxyProvider("DemoService");

            _testinghost.Host.MicrodotHostingConfigMock.StatusEndpoints = 
                new List<string>(new []{"/status"});
            
            _testinghost.Host.MicrodotHostingConfigMock.ShouldLogStatusEndpoint = false;
            
            var httpClient = new HttpClient();

            var uri = $"http://localhost:{_testinghost.BasePort}/status";

            var response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            
            uri = $"http://localhost:{_testinghost.BasePort}/some/status";
            
            response = await httpClient.GetAsync(uri);
            Assert.NotNull(response);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
