namespace Metrics.PerfCounters
{
    public interface IPerformanceCounterGauge
    {
        double GetValue(bool resetMetric = false);
        double Value { get; }
    }
}
