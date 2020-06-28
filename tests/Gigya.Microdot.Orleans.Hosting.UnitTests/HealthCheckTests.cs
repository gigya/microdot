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
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Service;
using Gigya.Microdot.Testing.Shared.Service;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class HealthCheckTests
    {
        private ServiceTester<CalculatorServiceHost> _tester;
        private int BasePort => _tester.Host.Arguments.BasePortOverride.Value;

        [OneTimeSetUp]
        public void SetUp()
        {
            _tester = new ServiceTester<CalculatorServiceHost>();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _tester.Dispose();
        }

        [Test]
        public async Task HealthCheck_ServcieDrain_StatueShouldBe521()
        {
            int port = DisposablePort.GetPort().Port;
            
            //serviceDrainTimeSec:
            var serviceArguments = new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive,
                ConsoleOutputMode.Disabled,
                SiloClusterMode.PrimaryNode, port, serviceDrainTimeSec: 1, instanceName: "test", initTimeOutSec: 10);

            var customServiceTester = new ServiceTester<CalculatorServiceHost>(
                serviceArguments: serviceArguments);

            var dispose = Task.Run(() => customServiceTester.Dispose());
            await Task.Delay(200);

            var httpResponseMessage = await new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:{port}/{nameof(IProgrammableHealth).Substring(1)}.status"));
            httpResponseMessage.StatusCode.ShouldBe((HttpStatusCode)521);
            await dispose;
        }

        [Test]
        public void HealthCheck_NotHealthy_ShouldReturn500()
        {
            _tester.GrainClient.GetGrain<IProgrammableHealthGrain>(0).SetHealth(false);
            var httpResponseMessage = new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:{BasePort}/{nameof(IProgrammableHealth).Substring(1)}.status")).Result;
            httpResponseMessage.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }


        [Test]
        public void HealthCheck_Healthy_ShouldReturn200()
        {
            _tester.GrainClient.GetGrain<IProgrammableHealthGrain>(0).SetHealth(true);
            var httpResponseMessage = new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:{BasePort}/{nameof(IProgrammableHealth).Substring(1)}.status")).Result;
            httpResponseMessage.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Test]
        public void HealthCheck_NotImplemented_ShouldReturn200()
        {
            var httpResponseMessage = new HttpClient().GetAsync(new Uri($"http://{CurrentApplicationInfo.HostName}:{BasePort}/{nameof(ICalculatorService).Substring(1)}.status")).Result;
            httpResponseMessage.StatusCode.ShouldBe(HttpStatusCode.OK);
            httpResponseMessage.Content.ShouldNotBeNull();
        }
    }
}
