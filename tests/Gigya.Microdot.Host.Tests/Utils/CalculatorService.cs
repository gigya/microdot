using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.Host.Tests.Utils
{
    [HttpService(12323)]
    public interface ICalculatorService
    {
        Task<int> Add(int a, int b);
    }

    public class CalculatorService : ICalculatorService
    {
        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }
    }

    public class CalculatorKernelConfig : KernelConfigurator<ICalculatorService>
    {
        protected override void Configure(IKernel kernel, BaseCommonConfig baseCommonConfig)
        {
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
            kernel.Bind<ICalculatorService>().To<CalculatorService>().InSingletonScope();
        }

        public override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
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
