using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHostWithSiloHostFake : CalculatorServiceHost
    {
        private IKernel _kernel;
        protected override void PreConfigure(IKernel kernel)
        {
            base.PreConfigure(kernel);

            _kernel = kernel;
          //  kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>();

            kernel.Rebind<IDependantClassFake>().To<DependantClassFake>().InTransientScope();
         //   kernel.Rebind<ILog>().To<NullLog>();
         //   kernel.Rebind<IServiceInterfaceMapper>().To<OrleansServiceInterfaceMapper>();
         //   kernel.Rebind<IAssemblyProvider>().To<AssemblyProvider>();

        }




    }
}
