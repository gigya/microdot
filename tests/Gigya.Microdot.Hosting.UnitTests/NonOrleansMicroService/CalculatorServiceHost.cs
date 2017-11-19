using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        protected override string ServiceName { get; }= "ICalculatorService";

        protected override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules(false);
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
          

            kernel.Bind<ICalculatorService>().To<CalculatorService>().InSingletonScope();
        }

   
    }
}