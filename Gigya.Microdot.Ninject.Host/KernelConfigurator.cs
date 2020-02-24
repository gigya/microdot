using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using Ninject;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gigya.Microdot.Ninject.Host
{
    public abstract class KernelConfigurator<TInterface> : IKernelConfigurator
    {
        public KernelConfigurator()
        {
            if (typeof(TInterface).IsInterface == false)
                throw new ArgumentException($"The specified type provided for the {nameof(TInterface)} generic argument must be an interface.");
        }

        public abstract ILoggingModule GetLoggingModule();

        public void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            kernel.Rebind<IActivator>().To<InstanceBasedActivator<TInterface>>().InSingletonScope();
            kernel.Rebind<IServiceInterfaceMapper>().To<IdentityServiceInterfaceMapper>().InSingletonScope().WithConstructorArgument(typeof(TInterface));

            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();

            kernel.Bind<IRequestListener>().To<HttpServiceListener>();

            GetLoggingModule().Bind(kernel.Rebind<ILog>(), kernel.Rebind<IEventPublisher>(), kernel.Rebind<Func<string, ILog>>());
            kernel.Rebind<ServiceArguments>().ToConstant(Arguments).InSingletonScope();
        }
        public void Configure(IKernel kernel)
        {
            
            this.Configure(kernel, kernel.Get<BaseCommonConfig>());
        }

        protected abstract void Configure(IKernel kernel, BaseCommonConfig commonConfig);


        public void PreInitialize(IKernel kernel)
        {
            kernel.Get<SystemInitializer.SystemInitializer>().Init();

            IWorkloadMetrics workloadMetrics = kernel.Get<IWorkloadMetrics>();
            workloadMetrics.Init();

            var metricsInitializer = kernel.Get<IMetricsInitializer>();
            metricsInitializer.Init();
        }


        public virtual void OnInitilize(IKernel kernel)
        {
        }


        public void Warmup(IKernel kernel)
        {
            throw new NotImplementedException();
        }
    }
}
