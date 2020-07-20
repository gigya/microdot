using Metrics.ConcurrencyUtilities;
using Metrics.MetricData;
using Metrics.Utils;
using System;

namespace Metrics.Core
{
    public class SimpleMeter
    {
        private const long NanosInSecond = 1000L * 1000L * 1000L;
        private const long IntervalSeconds = 5L;
        private const double Interval = IntervalSeconds * NanosInSecond;
        private const double SecondsPerMinute = 60.0;
        private const int OneMinute = 1;
        private const int FiveMinutes = 5;
        private const int FifteenMinutes = 15;
        private static readonly double M1Alpha = 1 - Math.Exp(-IntervalSeconds / SecondsPerMinute / OneMinute);
        private static readonly double M5Alpha = 1 - Math.Exp(-IntervalSeconds / SecondsPerMinute / FiveMinutes);
        private static readonly double M15Alpha = 1 - Math.Exp(-IntervalSeconds / SecondsPerMinute / FifteenMinutes);

        private readonly StripedLongAdder uncounted = new StripedLongAdder();

        private AtomicLong total = new AtomicLong(0L);
        private VolatileDouble m1Rate = new VolatileDouble(0.0);
        private VolatileDouble m5Rate = new VolatileDouble(0.0);
        private VolatileDouble m15Rate = new VolatileDouble(0.0);
        private volatile bool initialized;

        public void Mark(long count)
        {
            this.uncounted.Add(count);
        }

        public void Tick()
        {
            var count = this.uncounted.GetAndReset();
            Tick(count);
        }

        private void Tick(long count)
        {
            this.total.Add(count);
            var instantRate = count / Interval;
            if (this.initialized)
            {
                var rate = this.m1Rate.GetValue();
                this.m1Rate.SetValue(rate + M1Alpha * (instantRate - rate));

                rate = this.m5Rate.GetValue();
                this.m5Rate.SetValue(rate + M5Alpha * (instantRate - rate));

                rate = this.m15Rate.GetValue();
                this.m15Rate.SetValue(rate + M15Alpha * (instantRate - rate));
            }
            else
            {
                this.m1Rate.SetValue(instantRate);
                this.m5Rate.SetValue(instantRate);
                this.m15Rate.SetValue(instantRate);
                this.initialized = true;
            }
        }

        public void Reset()
        {
            this.uncounted.Reset();
            this.total.SetValue(0L);
            this.m1Rate.SetValue(0.0);
            this.m5Rate.SetValue(0.0);
            this.m15Rate.SetValue(0.0);
        }

        public MeterValue GetValue(double elapsed)
        {
            var count = this.total.GetValue() + this.uncounted.GetValue();
            return new MeterValue(count, GetMeanRate(count, elapsed), OneMinuteRate, FiveMinuteRate, FifteenMinuteRate, TimeUnit.Seconds);
        }

        private static double GetMeanRate(long value, double elapsed)
        {
            if (value == 0)
            {
                return 0.0;
            }

            return value / elapsed * TimeUnit.Seconds.ToNanoseconds(1);
        }

        private double FifteenMinuteRate { get { return this.m15Rate.GetValue() * NanosInSecond; } }
        private double FiveMinuteRate { get { return this.m5Rate.GetValue() * NanosInSecond; } }
        private double OneMinuteRate { get { return this.m1Rate.GetValue() * NanosInSecond; } }
    }
}
