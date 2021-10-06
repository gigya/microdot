using Metrics.MetricData;
using System;
using System.Runtime.InteropServices;

namespace Metrics.PerfCounters
{
    public class PerformanceCounterGauge : MetricValueProvider<double>
    {
        private readonly IPerformanceCounterGauge _performanceCounterGauge;

        public PerformanceCounterGauge(string category, string counter)
            : this(category, counter, instance: null)
        { }

        public PerformanceCounterGauge(string category, string counter, string instance)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                _performanceCounterGauge = new PerformanceCounterGaugeWindows(category, counter, instance);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                _performanceCounterGauge = new PerformanceCounterGaugeLinux(category, counter, instance);
            else 
                throw new NotSupportedException($"Platform '{RuntimeInformation.OSDescription}' not supported");
        }
        
        public double GetValue(bool resetMetric = false)
        {
            return _performanceCounterGauge.GetValue(resetMetric);
        }

        public double Value => _performanceCounterGauge.Value;
    }
}
