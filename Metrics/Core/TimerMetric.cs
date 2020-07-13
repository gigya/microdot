using System;
using Metrics.ConcurrencyUtilities;
using Metrics.MetricData;
using Metrics.Sampling;
using Metrics.Utils;

namespace Metrics.Core
{
    public interface TimerImplementation : Timer, MetricValueProvider<TimerValue> { }

    public sealed class TimerMetric : TimerImplementation, IDisposable
    {
        private readonly Clock clock;
        private readonly MeterImplementation meter;
        private readonly HistogramImplementation histogram;
        private readonly StripedLongAdder activeSessionsCounter = new StripedLongAdder();
        private readonly StripedLongAdder totalRecordedTime = new StripedLongAdder();

        public TimerMetric()
            : this(new HistogramMetric(), new MeterMetric(), Clock.Default) { }

        public TimerMetric(SamplingType samplingType)
            : this(new HistogramMetric(samplingType), new MeterMetric(), Clock.Default) { }

        public TimerMetric(HistogramImplementation histogram)
            : this(histogram, new MeterMetric(), Clock.Default) { }

        public TimerMetric(Reservoir reservoir)
            : this(new HistogramMetric(reservoir), new MeterMetric(), Clock.Default) { }

        public TimerMetric(SamplingType samplingType, MeterImplementation meter, Clock clock)
            : this(new HistogramMetric(samplingType), meter, clock) { }

        public TimerMetric(HistogramImplementation histogram, MeterImplementation meter, Clock clock)
        {
            this.clock = clock;
            this.meter = meter;
            this.histogram = histogram;
        }

        public void Record(long duration, TimeUnit unit, string userValue = null)
        {
            var nanos = unit.ToNanoseconds(duration);
            if (nanos >= 0)
            {
                this.histogram.Update(nanos, userValue);
                this.totalRecordedTime.Add(nanos);
                // Do not save user value in meter, because its only purpose is to provide overall rate metrics.
                // Unlike meter, user values for histogram are not restricted to come from a finite set, and generally they are unique.
                // Saving them will cause huge number of sub-meter-per-item allocations inside a meter metric. 
                // Moreover those sub-meters are useless, since rates-per-item are not reported for timers.
                this.meter.Mark();
            }
        }

        public void Time(Action action, string userValue = null)
        {
            var start = this.clock.Nanoseconds;
            try
            {
                this.activeSessionsCounter.Increment();
                action();
            }
            finally
            {
                this.activeSessionsCounter.Decrement();
                Record(this.clock.Nanoseconds - start, TimeUnit.Nanoseconds, userValue);
            }
        }

        public T Time<T>(Func<T> action, string userValue = null)
        {
            var start = this.clock.Nanoseconds;
            try
            {
                this.activeSessionsCounter.Increment();
                return action();
            }
            finally
            {
                this.activeSessionsCounter.Decrement();
                Record(this.clock.Nanoseconds - start, TimeUnit.Nanoseconds, userValue);
            }
        }

        public long StartRecording()
        {
            this.activeSessionsCounter.Increment();
            return this.clock.Nanoseconds;
        }

        public long CurrentTime()
        {
            return this.clock.Nanoseconds;
        }

        public long EndRecording()
        {
            this.activeSessionsCounter.Decrement();
            return this.clock.Nanoseconds;
        }

        public TimerContext NewContext(string userValue = null)
        {
            return new TimerContext(this, userValue);
        }

        public TimerValue Value
        {
            get
            {
                return GetValue();
            }
        }

        public TimerValue GetValue(bool resetMetric = false)
        {
            var total = resetMetric ? this.totalRecordedTime.GetAndReset() : this.totalRecordedTime.GetValue();
            return new TimerValue(this.meter.GetValue(resetMetric), this.histogram.GetValue(resetMetric), this.activeSessionsCounter.GetValue(), total, TimeUnit.Nanoseconds);
        }

        public void Reset()
        {
            this.meter.Reset();
            this.histogram.Reset();
        }

        public void Dispose()
        {
            using (this.histogram as IDisposable) { }
            using (this.meter as IDisposable) { }
        }
    }
}
