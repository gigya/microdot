using System;
using System.Collections.Generic;
using System.Diagnostics;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Ninject;

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
        
            kernel.Rebind<ILog>().ToConstant(new ConsoleLog { MinimumTraceLevel = eventType });
        
        return kernel;
    }
}