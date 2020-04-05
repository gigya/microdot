using System.Collections.Generic;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Configuration;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        public string ServiceName => this.Host.HostConfiguration.ApplicationInfo.Name;
        
        public IKernel Kernel;

        public CalculatorServiceHost() : base(
            new HostConfiguration(
                new TestHostConfigurationSource(appName: "ICalculatorService")))
        {
        }

        public CalculatorServiceHost(HostConfiguration hostConfiguration) : base(hostConfiguration)
        {
        }

        public override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
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