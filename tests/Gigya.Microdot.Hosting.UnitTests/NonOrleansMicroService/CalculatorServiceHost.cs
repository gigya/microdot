using System.Collections.Generic;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.UnitTests.Caching.Host;
using Ninject;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        public override string ServiceName { get; } = "ICalculatorService";

        protected override ILoggingModule GetLoggingModule()
        {
            return new ConsoleLogLoggersModules();
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {

            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
            kernel.Bind<ICalculatorService>().To<CalculatorService>().InSingletonScope();
        }

        public class MockServiceValidator : ServiceValidator
        {
            public MockServiceValidator()
                : base(new List<IValidator>().ToArray())
            {

            }
        }
    }
}