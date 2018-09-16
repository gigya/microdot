using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Ninject;
using NSubstitute;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHost : CalculatorServiceHost
    {
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            base.Configure(kernel, commonConfig);

            kernel.Rebind<IDependantClassFake>().To<DependantClassFake>().InSingletonScope();
        }
    }
}
