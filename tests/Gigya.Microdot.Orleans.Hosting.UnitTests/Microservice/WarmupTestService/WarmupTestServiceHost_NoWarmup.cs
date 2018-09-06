using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Testing;
using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHost_NoWarmup : WarmupTestServiceHost
    {
        protected override void Warmup(IKernel kernel)
        {
        }
    }
}
