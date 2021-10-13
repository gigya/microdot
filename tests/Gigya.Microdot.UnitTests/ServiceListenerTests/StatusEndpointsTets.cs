using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing.Shared.Service;
using Metrics;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{
    [TestFixture]
    public class ConfigurableHost<T>:TestingHost<T> where T:class
    {
        public MicrodotHostingConfig MicrodotHostingConfigMock = new MicrodotHostingConfig();
        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Rebind<Func<MicrodotHostingConfig>>().ToMethod(_ => ()=> MicrodotHostingConfigMock);
            
        }
    }
    
    public class StatusEndpointsTets
    {
        private NonOrleansServiceTester<ConfigurableHost<IDemoService>> _testinghost;

        [SetUp]
        public virtual void SetUp()
        {
            _testinghost = new NonOrleansServiceTester<ConfigurableHost<IDemoService>>();


            Metric.ShutdownContext("Service");
            TracingContext.SetRequestID("1");
        }

        [Test]
        public async Task StatusEndpiontsShouldLog()
        {
            var loggerSub = Substitute.For<ILog>();
            var microdotHostingConfig = new MicrodotHostingConfig();

            var statusEndpoints = new StatusEndpoints(() => microdotHostingConfig, loggerSub);

            List<(string data, HttpStatusCode status, string type)> writes =
                new List<(string data, HttpStatusCode status, string type)>(); 
                
            await statusEndpoints.TryHandle(null, (data, status, type) =>
            {
                writes.Add((data,status,type));
                return Task.CompletedTask;
            });

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
    }
}
