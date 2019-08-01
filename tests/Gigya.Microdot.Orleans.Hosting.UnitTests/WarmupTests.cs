using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.Testing.Service;
using Gigya.Microdot.Testing.Shared;
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

        [Test]
        public async Task ShouldNotWarmupGrainTestingConstructor()
        {
            TestingKernel<ConsoleLog> kernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<TestGrain>().ToSelf();
                k.Rebind<UsualGrain>().ToSelf();
                k.Rebind<IWarmup>().To<GrainsWarmup>().InSingletonScope();
                k.Rebind<IServiceInterfaceMapper>().To<OrleansServiceInterfaceMapper>().InSingletonScope();
            });

            IWarmup grainsWarmup = kernel.Get<IWarmup>();
            grainsWarmup.Warmup();
        }
    }
}
