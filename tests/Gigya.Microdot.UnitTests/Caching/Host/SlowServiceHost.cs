using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.HttpService;
using Ninject;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class SlowServiceHost : MicrodotServiceHost<ISlowService>
    {
        public string ServiceName => nameof(ISlowService).Substring(1);
        protected override ILoggingModule GetLoggingModule() { return new ConsoleLogLoggersModules(); }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Bind<ISlowService>().To<SlowService>().InSingletonScope();
            kernel.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();
            kernel.RebindForTests();
        }
    }
}
