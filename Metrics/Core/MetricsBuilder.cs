
using System;
using Metrics.MetricData;
using Metrics.Sampling;

namespace Metrics.Core
{
    public interface MetricsBuilder
    {
        MetricValueProvider<double> BuildPerformanceCounter(string name, Unit unit, string counterCategory, string counterName, string counterInstance);
        MetricValueProvider<double> BuildGauge(string name, Unit unit, Func<double> valueProvider);
        CounterImplementation BuildCounter(string name, Unit unit);
        MeterImplementation BuildMeter(string name, Unit unit, TimeUnit rateUnit);
        HistogramImplementation BuildHistogram(string name, Unit unit, SamplingType samplingType);
        HistogramImplementation BuildHistogram(string name, Unit unit, Reservoir reservoir);
        TimerImplementation BuildTimer(string name, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, SamplingType samplingType);
        TimerImplementation BuildTimer(string name, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, HistogramImplementation histogram);
        TimerImplementation BuildTimer(string name, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, Reservoir reservoir);
    }
}
