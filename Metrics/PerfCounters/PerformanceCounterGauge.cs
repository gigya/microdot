using Metrics.MetricData;
using System;
using System.Diagnostics;
using System.Security.Principal;

namespace Metrics.PerfCounters
{
    public class PerformanceCounterGauge : MetricValueProvider<double>
    {
        private readonly PerformanceCounter performanceCounter;

        public PerformanceCounterGauge(string category, string counter)
            : this(category, counter, instance: null)
        { }

        public PerformanceCounterGauge(string category, string counter, string instance)
        {
            try
            {
                this.performanceCounter = instance == null ?
                    new PerformanceCounter(category, counter, true) :
                    new PerformanceCounter(category, counter, instance, true);
                Metric.Internal.Counter("Performance Counters", Unit.Custom("Perf Counters")).Increment();
            }
            catch (Exception x)
            {
                var message = "Error reading performance counter data. The application is currently running as user " + GetIdentity() +
                    ". Make sure the user has access to the performance counters. The user needs to be either Admin or belong to Performance Monitor user group.";
                MetricsErrorHandler.Handle(x, message);
            }
        }

        private static string GetIdentity()
        {
            try
            {
                return Environment.UserName;
            }
            catch (Exception x)
            {
                return "[Unknown user | " + x.Message + " ]";
            }
        }

        public double GetValue(bool resetMetric = false)
        {
            return this.Value;
        }

        public double Value
        {
            get
            {
                try
                {
                    return this.performanceCounter?.NextValue() ?? double.NaN;
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
