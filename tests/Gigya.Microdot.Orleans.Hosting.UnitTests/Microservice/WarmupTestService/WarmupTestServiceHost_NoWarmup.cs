using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Testing;
using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHost_NoWarmup : WarmupTestServiceHost
    {
        public IWarmup WarmapMock
        {
            get;
            set;
        }

        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            base.Configure(kernel, commonConfig);
        }

        protected override void Warmup(IKernel kernel)
        {
            if (WarmapMock == null)
            {
                return;
            }

            kernel.Rebind<IWarmup>().ToConstant(WarmapMock);
        }
    }
}
