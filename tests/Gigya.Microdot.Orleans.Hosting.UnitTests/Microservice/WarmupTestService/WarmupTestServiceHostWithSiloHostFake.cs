using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHostWithSiloHostFake : CalculatorServiceHost
    {
        protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            base.PreConfigure(kernel, Arguments);
            kernel.Rebind<ISingletonDependency>().To<SingletonDependency>().InSingletonScope();
        }
    }
}
