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

        [TearDown]
        public void TearDown()
        {
            DependantClassFake.ResetWarmedTimes();
        }
        
        [Test]
        public async Task InstanceReadyBeforeCallingMethod_Warmup()
        {
            ServiceTester<CalculatorServiceHost> tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<CalculatorServiceHost>(mainPort);
            
            IWarmupTestServiceGrain grain = tester.GetGrainClient<IWarmupTestServiceGrain>(0);
            int result = await grain.TestWarmedTimes();
            result = await grain.TestWarmedTimes();
            result = await grain.TestWarmedTimes();

            Assert.AreEqual(result, 1);

            tester.Dispose();
        }

        [Test][Repeat(2)]
        public async Task VerifyWarmupBeforeSiloStart()
        {
            WarmupTestServiceHostWithSiloHostFake host = new WarmupTestServiceHostWithSiloHostFake();
            Task.Run(() => host.Run());
            await host.WaitForHostDisposed();
        }
    }
}
