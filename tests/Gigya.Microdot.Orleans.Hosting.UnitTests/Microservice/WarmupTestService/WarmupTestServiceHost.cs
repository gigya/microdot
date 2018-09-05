using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Orleans.Ninject.Host;
using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHost : CalculatorServiceHost
    {
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            base.Configure(kernel, commonConfig);

            kernel.Rebind<DependantClass>().ToSelf().InSingletonScope();
        }
    }
}
