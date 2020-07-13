using Metrics.MetricData;
using System;
using System.Collections.Generic;

namespace Metrics.Core
{
    public sealed class NullMetricsRegistry : MetricsRegistry
    {
        private struct NullMetric : Counter, Meter, Histogram, Timer, RegistryDataProvider
        {
            public static readonly NullMetric Instance = new NullMetric();
            private static readonly TimerContext NullContext = new TimerContext(NullMetric.Instance, null);

            public void Increment() { }
            public void Increment(long value) { }
            public void Decrement() { }
            public void Decrement(long value) { }
            public void Increment(string item) { }
            public void Increment(string item, long value) { }
            public void Decrement(string item) { }
            public void Decrement(string item, long value) { }
            public void Mark() { }
            public void Mark(long count) { }
            public void Mark(string item) { }
            public void Mark(string item, long count) { }

            public void Update(long value, string userValue) { }

            public long EndRecording() { return 0; }

            public void Record(long time, TimeUnit unit, string userValue = null) { }
            public void Time(Action action, string userValue = null) { action(); }
            public T Time<T>(Func<T> action, string userValue = null) { return action(); }
            public long StartRecording() { return 0; }
            public long CurrentTime() { return 0; }

            public TimerContext NewContext(string userValue = null) { return NullContext; }

            public void Reset() { }

            public IEnumerable<GaugeValueSource> Gauges { get { yield break; } }
            public IEnumerable<CounterValueSource> Counters { get { yield break; } }
            public IEnumerable<MeterValueSource> Meters { get { yield break; } }
            public IEnumerable<HistogramValueSource> Histograms { get { yield break; } }
            public IEnumerable<TimerValueSource> Timers { get { yield break; } }
        }

        public RegistryDataProvider DataProvider => NullMetric.Instance;

        public void ClearAllMetrics() { }
        public void ResetMetricsValues() { }

        public void Gauge(string name, Func<MetricValueProvider<double>> valueProvider, Unit unit, MetricTags tags) { }

        public Counter Counter<T>(string name, Func<T> builder, Unit unit, MetricTags tags) where T : CounterImplementation
        {
            return NullMetric.Instance;
        }

        public Meter Meter<T>(string name, Func<T> builder, Unit unit, TimeUnit rateUnit, MetricTags tags) where T : MeterImplementation
        {
            return NullMetric.Instance;
        }

        public Histogram Histogram<T>(string name, Func<T> builder, Unit unit, MetricTags tags) where T : HistogramImplementation
        {
            return NullMetric.Instance;
        }

        public Timer Timer<T>(string name, Func<T> builder, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags) where T : TimerImplementation
        {
            return NullMetric.Instance;
        }
    }
}
