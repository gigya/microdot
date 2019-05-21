using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
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
            kernel.Rebind<IDependantClassFake>().To<DependantClassFake>().InTransientScope();
         

        }




    }
}
