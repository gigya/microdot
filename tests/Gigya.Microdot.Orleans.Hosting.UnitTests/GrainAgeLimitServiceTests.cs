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
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Testing.Service;
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

        [Test]
        public async Task WithNoneAgeLimitTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WithNoneAgeLimitServiceHost>(basePortOverride: 6454, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            service.SendFake("").Result.ShouldBeTrue();

        }

        [Test]
        public async Task WithAgeLimitTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WithAgeLimitServiceHost>(basePortOverride: 6354, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            service.SendFake("").Result.ShouldBeTrue();
        }
        
        [Test]
        public async Task WithInvalidAgeLimitTest_ThrowArgumentException()
        {
            Should.Throw<ArgumentException>(() => AssemblyInitialize.ResolutionRoot.GetServiceTester<WithInvalidAgeLimitServiceHost>(basePortOverride: 6254, writeLogToFile: true));
        }

        [Description("Loading real configuration from GrainTestService")]
        [Test]
        public async Task GrainTestServiceTest()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<GrainTestServiceHost>(basePortOverride: 6154, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            service.SendFake("").Result.ShouldBeTrue();
        }

        [Ignore("The test run to long - Should think of a better way to test it.")]
        [Test]
        public async Task ForceDiactivationAfter10Seconds()
        {
            var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<With10SecondsAgeLimitServiceHost>(basePortOverride: 6454, writeLogToFile: true);
            Tester = tester;
            var service = tester.GetServiceProxy<IGarinAgeLimitService>();

            service.SendFake("").Result.ShouldBeTrue();
            service.WasCollected().Result.ShouldBeFalse();

            await Task.Delay(TimeSpan.FromSeconds(15));
            service.WasCollected().Result.ShouldBeFalse();


            await Task.Delay(TimeSpan.FromMinutes(2));
            service.WasCollected().Result.ShouldBeTrue();

        }


    }
}

