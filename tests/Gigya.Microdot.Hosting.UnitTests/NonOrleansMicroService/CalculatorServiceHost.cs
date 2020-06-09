using System.Collections.Generic;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.HttpService;
using Ninject;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        public IKernel Kernel;

        public CalculatorServiceHost() : base(
            new HostEnvironment(new TestHostEnvironmentSource(
                zone: "zone",
                deploymentEnvironment: "env",
                appName: "ICalculatorService")), 
            new System.Version())
        {
        }

        public CalculatorServiceHost(HostEnvironment environment, System.Version version)
            : base(environment, version) { }

        protected override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
            kernel.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();
            kernel.Bind<ICalculatorService>().To<CalculatorService>().InSingletonScope();

            this.Kernel = kernel;
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