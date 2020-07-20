using System.Collections.Generic;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces.SystemWrappers;
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
        private readonly HostEnvironment environment;

        public override string ServiceName => "ICalculatorService";

        protected override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
        }

        public CalculatorServiceHost()
        {

        }

        public CalculatorServiceHost(HostEnvironment environment)
        {
            this.environment = environment;
        }

        protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            var env = environment ?? new HostEnvironment(new TestHostEnvironmentSource(
                zone: "zone",
                deploymentEnvironment: "env",
                appName: "ICalculatorService"));

            kernel.Rebind<IEnvironment>().ToConstant(env).InSingletonScope();
            kernel.Rebind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();

            base.PreConfigure(kernel, Arguments);
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