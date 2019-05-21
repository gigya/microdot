using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService;
using Gigya.Microdot.Testing.Service;
using Ninject;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class WarmupTests
    {
        private int mainPort = 9555;

        [SetUp]
        public void SetUp()
        {
            DependantClassFake.ResetWarmedTimes();
        }

        [Test]
        public async Task InstanceReadyBeforeCallingMethod_Warmup()
        {
            ServiceTester<WarmupTestServiceHostWithSiloHostFake> tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WarmupTestServiceHostWithSiloHostFake>(mainPort);
            IWarmupTestServiceGrain grain = tester.GrainClient.GetGrain<IWarmupTestServiceGrain>(0);
          
            int result = await grain.TestWarmedTimes();

            Assert.AreEqual(result, 2);

            tester.Dispose();
        }

        [Test]
        [Repeat(1)]
        public async Task VerifyWarmupBeforeSiloStart()
        {
           using ( var tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<WarmupTestServiceHostWithSiloHostFake>(mainPort))
           
            Assert.AreEqual(DependantClassFake.WarmedTimes, 1);
   
        }
    }
}
