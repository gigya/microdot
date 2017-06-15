using System.Linq;

using Metrics.MetricData;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests
{

    public static class MetricsDataExtenstion
    {
        public static void AssertEquals(this MetricsData metricsData, MetricsDataEquatable testData)
        {
            var counters = metricsData.Counters.Where(a => a.Value.Count > 0).OrderBy(a=>a.Name);
            var testCounters = testData.Counters.OrderBy(a=>a.Name);

            counters.Select(a => a.Name).ShouldBe(testCounters.Select(a => a.Name));
            counters.Select(a => a.Unit).ShouldBe(testCounters.Select(a => a.Unit));
            
            foreach (var counter in testCounters.Where(s=>s.SubCounters!=null))
            {                                
                metricsData.Counters.First(a => a.Name == counter.Name).Value.Items.Select(a => a.Item).ShouldBe(counter.SubCounters);              
            }

            if (testData.CountersSettings.CheckValues)
            {
                counters.Select(a => a.Value.Count).ShouldBe(testCounters.Select(a => a.Value));
            }

            //Timers
            var timers = metricsData.Timers.Where(a => a.Value.Histogram.Count > 0).OrderBy(a=>a.Name);
            var testTimers = testData.Timers.OrderBy(a=>a.Name);

            var timerNames = timers.Select(a => a.Name).ToArray();
            var testTimersExpected = testTimers.Select(a => a.Name).ToArray();

            timerNames.ShouldBe(testTimersExpected, $"Timers not recorded. In context {metricsData.Context}");
            

            Assert.That(timers.Select(a => a.Unit), Is.EquivalentTo(testTimers.Select(a => a.Unit)), $"Timers units not correct. In context {metricsData.Context}");

            if (testData.TimersSettings.CheckValues)
            {
                Assert.That(timers.Select(a => a.Value.Histogram.Count), Is.EquivalentTo(testTimers.Select(a => a.Value)), $"Timers values not correct. In context {metricsData.Context}");
            }

            //Gauges
            var gauges = metricsData.Gauges.OrderBy(a=>a.Name);
            var testGauges = testData.Gauges.OrderBy(a => a.Name);

            Assert.That(gauges.Select(a => a.Name), Is.EquivalentTo(testGauges.Select(a => a.Name)), $"Gauges not recorded. In context {metricsData.Context}");

            Assert.That(gauges.Select(a => a.Unit), Is.EquivalentTo(testGauges.Select(a => a.Unit)), $"Gauges units not correct. In context {metricsData.Context}");
            
            if (testData.GaugesSettings.CheckValues)
            {
                Assert.That(gauges.Select(a => a.Value), Is.EquivalentTo(testGauges.Select(a => a.Value)), $"Gauges values not correct. In context {metricsData.Context}");
            }

            //Meters
            var meters = metricsData.Meters.Where(a => a.Value.Count > 0).OrderBy(a=>a.Name);
            var testMeters = testData.Meters.OrderBy(a => a.Name);

            Assert.That(meters.Select(a => a.Name), Is.EquivalentTo(testMeters.Select(a => a.Name)), $"Meters not recorded. In context {metricsData.Context}");

            Assert.That(meters.Select(a => a.Unit), Is.EquivalentTo(testMeters.Select(a => a.Unit)), $"Meters units not correct. In context {metricsData.Context}");
           
            if (testData.MetersSettings.CheckValues)
            {
                Assert.That(meters.Select(a => a.Value.Count), Is.EquivalentTo(testMeters.Select(a => a.Value)), $"Meters values not correct. In context {metricsData.Context}");
            }
        }
    }
}