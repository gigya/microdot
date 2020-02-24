using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using Ninject;
using System;
using System.Collections.Generic;
using System.Text;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    public abstract class OrleansKernelConfigurator : IKernelConfigurator
    {
        public void Configure(IKernel kernel)
        {
            this.Configure(kernel, kernel.Get<OrleansCodeConfig>());
        }

        protected virtual void Configure(IKernel kernel, OrleansCodeConfig commonConfig) { }

        public abstract ILoggingModule GetLoggingModule();

        public virtual void OnInitilize(IKernel kernel)
        {
        }

        public virtual void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();
            kernel.Load<MicrodotOrleansHostModule>();
            kernel.Rebind<ServiceArguments>().ToConstant(Arguments);

            kernel.Bind<IRequestListener>().To<GigyaSiloHost>();

            GetLoggingModule().Bind(kernel.Rebind<ILog>(), kernel.Rebind<IEventPublisher>(), kernel.Rebind<Func<string, ILog>>());
        }

        public void PreInitialize(IKernel kernel)
        {
            kernel.Get<SystemInitializer>().Init();

            IWorkloadMetrics workloadMetrics = kernel.Get<IWorkloadMetrics>();
            workloadMetrics.Init();

            var metricsInitializer = kernel.Get<IMetricsInitializer>();
            metricsInitializer.Init();
        }

        public void Warmup(IKernel kernel)
        {
            IWarmup warmup = kernel.Get<IWarmup>();
            warmup.Warmup();
        }
    }
}
