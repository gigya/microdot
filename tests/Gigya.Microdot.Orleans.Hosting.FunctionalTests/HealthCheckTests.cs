using System;
using System.Net;
using System.Net.Http;

using Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice;
using Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.ServiceTester;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests
{
    [TestFixture]
    public class HealthCheckTests
    {
        private ServiceTester<CalculatorServiceHost> tester;


        [OneTimeSetUp]
        public void SetUp()
        {
            tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<CalculatorServiceHost>();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            tester.Dispose();
        }

        [Test]
        public void HealthCheck_NotHealthy_ShouldReturn500()
        {
            tester.GetGrainClient<IProgrammableHealthGrain>(0).SetHealth(false);
            var httpResponseMessage = new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:6555/{nameof(IProgrammableHealth).Substring(1)}.status")).Result;
            httpResponseMessage.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        [Test]
        public void HealthCheck_Healthy_ShouldReturn200()
        {
            tester.GetGrainClient<IProgrammableHealthGrain>(0).SetHealth(true);
            var httpResponseMessage = new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:6555/{nameof(IProgrammableHealth).Substring(1)}.status")).Result;
            httpResponseMessage.StatusCode.ShouldBe(HttpStatusCode.OK); 
        }

        [Test]
        public void HealthCheck_NotImplemented_ShouldReturn200()
        {
            var httpResponseMessage = new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:6555/{nameof(ICalculatorService).Substring(1)}.status")).Result;
            httpResponseMessage.StatusCode.ShouldBe(HttpStatusCode.OK);
            httpResponseMessage.Content.ShouldNotBeNull();      
        }
    }
}
