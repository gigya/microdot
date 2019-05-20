using System;
using System.Collections.Generic;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class FakesLoggersModules : ILoggingModule
    {
        public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding, IBindingToSyntax<Func<string, ILog>> logFactory)
        {


            logBinding.To<ConsoleLog>();

            logFactory.ToMethod(c => caller => c.Kernel.Get<ConsoleLog>());
            eventPublisherBinding.To<NullEventPublisher>();
        }
    }

    public class SlowServiceHost : MicrodotServiceHost<ISlowService>
    {
        public IKernel Kernel; 
     

        protected override ILoggingModule GetLoggingModule() { return new FakesLoggersModules(); }
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
