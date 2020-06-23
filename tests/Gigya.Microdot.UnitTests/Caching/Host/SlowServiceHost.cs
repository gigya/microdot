using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.HttpService;
using Ninject;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class SlowServiceHost : MicrodotServiceHost<ISlowService>
    {
        public override string ServiceName => nameof(ISlowService).Substring(1);
        protected override ILoggingModule GetLoggingModule() { return new ConsoleLogLoggersModules(); }


        protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            var env = new HostEnvironment(new TestHostEnvironmentSource(appName: ServiceName));
            kernel.Rebind<IEnvironment>().ToConstant(env).InSingletonScope();
            kernel.Rebind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();
            base.PreConfigure(kernel, Arguments);
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Bind<ISlowService>().To<SlowService>().InSingletonScope();
            kernel.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();
            kernel.RebindForTests();
        }
    }
}
