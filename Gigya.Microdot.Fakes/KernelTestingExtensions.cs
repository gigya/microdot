using System;
using System.Diagnostics;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
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
            return kernel.WithNoMetrics().WithLogForTests();
        }

        public static IKernel WithLogForTests(this IKernel kernel, TraceEventType eventType = TraceEventType.Warning)
        {

            var useServiceTester = AppDomain.CurrentDomain.GetData("HttpLogListenPort");
            if (useServiceTester is int)
            {
                kernel.Rebind<ILog>().ToConstant(new HttpLog(TraceEventType.Warning));
            }
            else
            {
                kernel.Rebind<ILog>().ToConstant(new ConsoleLog {MinimumTraceLevel = eventType});
            }

            return kernel;
        }


    }
}