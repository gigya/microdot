using System;
using System.Diagnostics;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.HttpService;
using Ninject;


namespace Gigya.Microdot.Fakes.KernelUtils
{
    public static class KernelTestingExtensions
    {
        /// <summary>
        /// Disable Metrics 
        /// </summary>
        public static IKernel WithNoMetrics(this IKernel kernel)
        {
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>().InSingletonScope();
            return kernel;
        }

        public static IKernel RebindForTests(this IKernel kernel)
        {
            return kernel.WithNoMetrics().WithLogForTests().WithNoCrashHandler().WithNoCertificateStore();
        }
        public class NoCrashHandler : ICrashHandler
        {
            public void Init(Action signalClusterThatThisNodeIsGoingDown)
            {
                //  throw new NotImplementedException();
            }
        }
        public static IKernel WithNoCrashHandler(this IKernel kernel)
        {
            kernel.Rebind<ICrashHandler>().To<NoCrashHandler>().InSingletonScope();
            return kernel;
        }

        public static IKernel WithNoCertificateStore(this IKernel kernel)
        {
            kernel.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();
            return kernel;
        }

        public static IKernel WithLogForTests(this IKernel kernel, TraceEventType eventType = TraceEventType.Warning)
        {



            kernel.Rebind<ILog>().ToConstant(new ConsoleLog { MinimumTraceLevel = eventType });


            return kernel;
        }


    }
}