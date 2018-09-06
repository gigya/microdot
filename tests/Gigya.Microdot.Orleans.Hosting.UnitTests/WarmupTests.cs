using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Service;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class WarmupTests
    {
        private int mainPort = 9555;
        
        [Test]
        public async Task Warmup_InstanceReadyBeforeCallingMethod()
        {
            ServiceTester<WarmupTestServiceHost> tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WarmupTestServiceHost>(mainPort);


            IWarmupTestServiceGrain grain = tester.GetGrainClient<IWarmupTestServiceGrain>(0);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int result = await grain.Test();
            sw.Stop();

            Assert.Less(sw.Elapsed.TotalMilliseconds, AssemblyInitialize.ResolutionRoot.Get<DependantClass>().SleepTime);
            Assert.AreEqual(result, 2);

            tester.Dispose();
        }

        [Test]
        public async Task NoWarmup_InstanceNotReadyBeforeCallingMethod()
        {
            ServiceTester<WarmupTestServiceHost_NoWarmup> tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WarmupTestServiceHost_NoWarmup>(mainPort);

            IWarmupTestServiceGrain grain = tester.GetGrainClient<IWarmupTestServiceGrain>(0);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int result = await grain.Test();
            sw.Stop();

            Assert.Greater(sw.Elapsed.TotalMilliseconds, AssemblyInitialize.ResolutionRoot.Get<DependantClass>().SleepTime);
            Assert.AreEqual(result, 2);

            tester.Dispose();
        }
    }
}
