using Metrics.MetricData;
using System;

namespace Metrics.PerfCounters
{
    public class PerformanceCounterGaugeLinux : MetricValueProvider<double>, IPerformanceCounterGauge
    {
        public PerformanceCounterGaugeLinux(string category, string counter)
            : this(category, counter, instance: null)
        { }

        public PerformanceCounterGaugeLinux(string category, string counter, string instance)
        {
            try
            {
                Metric.Internal.Counter("Performance Counters", Unit.Custom("Perf Counters")).Increment();
            }
            catch (Exception x)
            {
                MetricsErrorHandler.Handle(x);
            }
        }

        public double GetValue(bool resetMetric = false)
        {
            return this.Value;
        }

        public double Value => double.NaN;
    }
}
