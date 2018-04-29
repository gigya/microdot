#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Service;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
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
        public async Task HealthCheck_ServcieDrain_StatueShouldBe521()
        {
            int port = 6755;//prevent prot collision, more then one silo is runing at the same time in this TestFixture.
            var customServiceTester = AssemblyInitialize.ResolutionRoot.GetServiceTester<CalculatorServiceHost>(basePortOverride: port, serviceDrainTime: TimeSpan.FromSeconds(10));

            var dispose = Task.Run(() => customServiceTester.Dispose());
         
            var httpResponseMessage = await new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:{port}/{nameof(IProgrammableHealth).Substring(1)}.status"));
            httpResponseMessage.StatusCode.ShouldBe((HttpStatusCode)521);
            await dispose;
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
