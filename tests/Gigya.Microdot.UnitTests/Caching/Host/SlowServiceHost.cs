using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class SlowServiceHost : MicrodotServiceHost<ISlowService>
    {
        public IKernel Kernel; 
     

        protected override ILoggingModule GetLoggingModule() { return new ConsoleLogLoggersModules(); }
        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>().InSingletonScope();
            kernel.Rebind<IDiscovery>().To<AlwaysLocalhostDiscovery>().InSingletonScope();
            kernel.Rebind<IDiscoverySourceLoader>().To<AlwaysLocalHost>().InSingletonScope();
        }

        protected override void PreInitialize(IKernel kernel)
        {
            Kernel = kernel;
            base.PreInitialize(kernel);

            kernel.Bind<ISlowService>().To<SlowService>().InSingletonScope();
        }
    }
}
