using Metrics.MetricData;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Metrics.PerfCounters
{
    public class PerformanceCounterGaugeWindows : MetricValueProvider<double>, IPerformanceCounterGauge
    {
        private readonly PerformanceCounter _performanceCounter;
        public PerformanceCounterGaugeWindows(string category, string counter)
            : this(category, counter, instance: null)
        { }

        public PerformanceCounterGaugeWindows(string category, string counter, string instance)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _performanceCounter = new PerformanceCounter(category, counter, instance ?? "", true);
                    Metric.Internal.Counter("Performance Counters", Unit.Custom("Perf Counters")).Increment();
                }
                catch (Exception x)
                {
                    var message =
                        "Error reading performance counter data. The application is currently running as user " +
                        GetIdentity() +
                        ". Make sure the user has access to the performance counters. The user needs to be either Admin or belong to Performance Monitor user group.";
                    MetricsErrorHandler.Handle(x, message);
                }
            }
        }

        private static string GetIdentity()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return WindowsIdentity.GetCurrent().Name;

                return Environment.UserName;
            }
            catch (Exception x)
            {
                return "[Unknown user | " + x.Message + " ]";
            }
        }

        public double GetValue(bool resetMetric = false)
        {
            return Value;
        }

        public double Value
        {
            get
            {
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        return _performanceCounter?.NextValue() ?? double.NaN;

                    return double.NaN;
                }
                catch (Exception x)
                {
                    var message = "Error reading performance counter data. The application is currently running as user " + GetIdentity() +
                        ". Make sure the user has access to the performance counters. The user needs to be either Admin or belong to Performance Monitor user group.";
                    MetricsErrorHandler.Handle(x, message);
                    return double.NaN;
                }
            }
        }
    }
}
