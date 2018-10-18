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
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class FakesLoggersModules : ILoggingModule
    {
        private readonly bool _useHttpLog;

        public FakesLoggersModules(bool useHttpLog)
        {
            _useHttpLog = useHttpLog;
        }

        public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding)
        {
            if (_useHttpLog)
                logBinding.To<HttpLog>();
            else
                logBinding.To<ConsoleLog>();

            eventPublisherBinding.To<NullEventPublisher>();
        }
    }

    public class SlowServiceHost : MicrodotServiceHost<ISlowService>
    {
        private readonly Action<IKernel> action;

        public SlowServiceHost(Action<IKernel> action = null)
        {
            this.action = action;
        }

        protected override ILoggingModule GetLoggingModule() { return new FakesLoggersModules(false); }
        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>().InSingletonScope();
            kernel.Rebind<IDiscovery>().To<AlwaysLocalhostDiscovery>().InSingletonScope();
            kernel.Rebind<IDiscoverySourceLoader>().To<AlwaysLocalHost>().InSingletonScope();
            
            kernel.Bind<ISlowService>().To<SlowService>().InSingletonScope();
        }

        protected override void PreInitialize(IKernel kernel)
        {
            base.PreInitialize(kernel);

            action?.Invoke(kernel);
        }
    }
}
