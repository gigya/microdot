
using Metrics.MetricData;
using Metrics.Utils;
using System.Collections.Generic;
using System.Linq;
namespace Metrics.Reporters
{
    public class CSVReport : BaseReport
    {
        public class Value
        {
            public Value(string name, bool value)
                : this(name, value ? "Yes" : "No")
            { }

            public Value(string name, double value)
                : this(name, value.ToString("F"))
            { }

            public Value(string name, long value)
                : this(name, value.ToString("D"))
            { }

            public Value(string name, string value)
            {
                this.Name = name;
                this.FormattedValue = value;
            }

            public string Name { get; private set; }
            public string FormattedValue { get; private set; }
        }

        private readonly CSVAppender appender;

        public CSVReport(CSVAppender appender)
        {
            this.appender = appender;
        }

        protected override void ReportGauge(string name, double value, Unit unit, MetricTags tags)
        {
            Write("Gauge", name, GaugeValues(value, unit));
        }

        protected override void ReportCounter(string name, CounterValue value, Unit unit, MetricTags tags)
        {
            Write("Counter", name, CounterValues(value.Count, unit));
        }

        protected override void ReportMeter(string name, MeterValue value, Unit unit, TimeUnit rateUnit, MetricTags tags)
        {
            Write("Meter", name, MeterValues(value, unit, rateUnit));
        }

        protected override void ReportHistogram(string name, HistogramValue value, Unit unit, MetricTags tags)
        {
            Write("Histogram", name, HistogramValues(value, unit));
        }

        protected override void ReportTimer(string name, TimerValue value, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags)
        {
            var values = MeterValues(value.Rate, unit, rateUnit)
                .Concat(HistogramValues(value.Histogram, unit, durationUnit))
                .Concat(new[]
                {
                    new Value("Active Sessions", value.ActiveSessions),
                    new Value("Total Time", value.TotalTime)
                });

            Write("Timer", name, values);
        }

        protected override void ReportHealth(HealthStatus status)
        {
            Write("All", "HealthChecks", new[] {
                new Value("All Healthy", status.IsHealthy) }.Union(
                status.Results.SelectMany(r => new[]
            {
                new Value(r.Name + " IsHealthy" ,r.Check.IsHealthy),
                new Value(r.Name + " Message" ,r.Check.Message.Split('\n').First() ) // only first line
            })));
        }

        private static IEnumerable<Value> GaugeValues(double gaugeValue, Unit unit)
        {
            yield return new Value("Value", gaugeValue);

            if (!string.IsNullOrEmpty(unit.Name))
            {
                yield return new Value("Unit", unit.Name);
            }
        }

        private static IEnumerable<Value> CounterValues(long counter, Unit unit)
        {
            yield return new Value("Count", counter);

            if (!string.IsNullOrEmpty(unit.Name))
            {
                yield return new Value("Unit", unit.Name);
            }
        }

        private static IEnumerable<Value> MeterValues(MeterValue meter, Unit unit, TimeUnit rateUnit)
        {
            yield return new Value("Count", meter.Count);
            yield return new Value("Mean Rate", meter.MeanRate);
            yield return new Value("One Minute Rate", meter.OneMinuteRate);
            yield return new Value("Five Minute Rate", meter.FiveMinuteRate);
            yield return new Value("Fifteen Minute Rate", meter.FifteenMinuteRate);

            if (!string.IsNullOrEmpty(unit.Name))
            {
                yield return new Value("Rate Unit", $"{unit.Name}/{rateUnit.Unit()}");
            }
        }

        private static IEnumerable<Value> HistogramValues(HistogramValue value, Unit unit, TimeUnit? timeUnit = null)
        {
            yield return new Value("Last", value.LastValue);
            yield return new Value("Last User Value", value.LastUserValue);
            yield return new Value("Min", value.Min);
            yield return new Value("Min User Value", value.MinUserValue);
            yield return new Value("Max", value.Max);
            yield return new Value("Max User Value", value.MaxUserValue);
            yield return new Value("Mean", value.Mean);
            yield return new Value("StdDev", value.StdDev);
            yield return new Value("Median", value.Median);
            yield return new Value("75%", value.Percentile75);
            yield return new Value("95%", value.Percentile95);
            yield return new Value("98%", value.Percentile98);
            yield return new Value("99%", value.Percentile99);
            yield return new Value("99.9%", value.Percentile999);

            if (timeUnit.HasValue)
            {
                yield return new Value("Unit", timeUnit.Value.Unit());
            }
            else
            {
                if (!string.IsNullOrEmpty(unit.Name))
                {
                    yield return new Value("Unit", unit.Name);
                }
            }
        }

        private void Write(string metricType, string metricName, IEnumerable<Value> values)
        {
            this.appender.AppendLine(CurrentContextTimestamp, metricType, metricName, values);
        }
    }
}
