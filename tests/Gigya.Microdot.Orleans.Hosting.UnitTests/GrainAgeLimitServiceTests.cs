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
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Testing.Service;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class GrainAgeLimitServiceTests
    {
        private IDisposable Tester { get; set; }

        [TearDown]
        public void TearDown()
        {
            Tester?.Dispose();
        }

        [Ignore("Run too much time.")]
        [Test]
        public async Task ChangeableAgeLimitTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<AgeLimitConfigUpdatesServiceHost>( writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGrainConfigAgeTesterService>(timeout:TimeSpan.FromMinutes(20));


            await service.SetDefaultAgeLimit();

            await Task.Delay(TimeSpan.FromMinutes(3.5));

            service.ValidateTimestamps().Result.ShouldBeTrue();
        }

        [Test]
        public async Task WithNoneAgeLimitTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WithNoneAgeLimitServiceHost>(basePortOverride: 6454, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            service.Activate("").Result.ShouldBeTrue();
        }

        [Test]
        public async Task WithAgeLimitTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WithAgeLimitServiceHost>(basePortOverride: 6354, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            service.Activate("").Result.ShouldBeTrue();
        }

        [Test]
        public async Task WithInvalidAgeLimitTest_ThrowArgumentException()
        {
            Should.Throw<ArgumentException>(() => AssemblyInitialize.ResolutionRoot.GetServiceTester<WithInvalidAgeLimitServiceHost>(basePortOverride: 6254, writeLogToFile: true));
        }

        [Description("Loading real configuration from GrainTestService")]
        //[Ignore("Require real config.")]
        [Test]
        public async Task GrainTestServiceTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<ReadingRealConfigurationServiceHost>(basePortOverride: 6154, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            await service.Activate("");

            //await Task.Delay(TimeSpan.FromMinutes(2));
            //var result = await service.VerifyWhetherCollected();
            //result.ShouldBeTrue();

            await Task.Delay(TimeSpan.FromMinutes(20));
        }
    }
}

