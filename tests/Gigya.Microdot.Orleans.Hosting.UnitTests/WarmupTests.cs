
using System;
using System.Threading.Tasks;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService;
using Gigya.Microdot.Testing.Service;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class WarmupTests
    {

        [Test]
        public async Task InstanceReadyBeforeCallingMethod_Warmup()
        {
            ServiceTester<WarmupTestServiceHostWithSiloHostFake> tester = new ServiceTester<WarmupTestServiceHostWithSiloHostFake>();
            var beforeGrainCreated = DateTime.Now;

            IWarmupTestServiceGrain grain = tester.GrainClient.GetGrain<IWarmupTestServiceGrain>(0);

            var dependencyCreateDate = await grain.DependencyCreateDate();

            Assert.Greater(beforeGrainCreated, dependencyCreateDate, "dependencyCreateDate should create before grain is created");

            tester.Dispose();
        }

    }
}
