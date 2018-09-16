using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Ninject;
using NSubstitute;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHostWithSiloHostFake : WarmupTestServiceHost
    {
        private IDependantClassFake _dependantClassFake = Substitute.For<IDependantClassFake>();

        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>();

            kernel.Rebind<GigyaSiloHost>().To<GigyaSiloHostFake>();
            kernel.Rebind<IDependantClassFake>().ToConstant(_dependantClassFake);
            kernel.Rebind<ILog>().To<NullLog>();
            kernel.Rebind<IServiceInterfaceMapper>().To<OrleansServiceInterfaceMapper>();
            kernel.Rebind<IAssemblyProvider>().To<AssemblyProvider>();

            ServiceArguments args = new ServiceArguments(basePortOverride:9555);
            kernel.Rebind<ServiceArguments>().ToConstant(args);
            kernel.Rebind<WarmupTestServiceHostWithSiloHostFake>().ToConstant(this);
        }

        public async Task StopTest()
        {
            await WaitForServiceStartedAsync();
            Stop();
        }
    }
}
