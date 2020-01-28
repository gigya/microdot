using System.Collections.Generic;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Ninject;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        public override string ServiceName { get; } = "ICalculatorService";
        public IKernel Kernel;

        public CalculatorServiceHost() : base(new HostConfiguration(new TestHostConfigurationSource(appName: "ICalculatorService")))
        {
        }

        public CalculatorServiceHost(HostConfiguration hostConfiguration) : base(hostConfiguration)
        {
        }

        protected override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            Kernel = kernel;
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