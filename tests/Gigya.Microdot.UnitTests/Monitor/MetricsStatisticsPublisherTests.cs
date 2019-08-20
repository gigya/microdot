using System.Linq;
using Gigya.Microdot.Orleans.Hosting;
using Metrics.MetricData;
using NUnit.Framework;
using Shouldly;
using Metric = Metrics.Metric;


namespace Gigya.Microdot.UnitTests.Monitor
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class MetricsStatisticsPublisherTests
    {
        MetricsStatisticsConsumer publisher;
    
        [SetUp]
        public void PublishStatistics()
        {
            Metric.ShutdownContext("Silo");  
            publisher = new MetricsStatisticsConsumer();
        }

        [Test]
        public void ClosePublisherTest()
        {
            publisher.Close();            
        }

        [Test]
        public void PublishCounterTest()
        {
            publisher.IncrementMetric("Counter", 100);
            GetGauge("Counter").Value.ShouldBe(100);
            
            publisher.IncrementMetric("Counter", 100);
            GetGauge("Counter").Value.ShouldBe(200);
            
            publisher.IncrementMetric("Counter", -100);
            GetGauge("Counter").Value.ShouldBe(100);            

            publisher.DecrementMetric("Counter", 100);
            GetGauge("Counter").Value.ShouldBe(0);
        }

        [Test]
        public void UpdateCounterTest()
        {
            publisher.TrackMetric("Counter1", 100);
            GetGauge("Counter1").Value.ShouldBe(100);

            publisher.TrackMetric("Counter1", 300);
            GetGauge("Counter1").Value.ShouldBe(300);
        }

        private static MetricsData GetMetricsData()
        {
            return Metric.Context("Silo").DataProvider.CurrentMetricsData;
        }

        private static GaugeValueSource GetGauge(string gaugeName)
        {
            return GetMetricsData().Gauges.FirstOrDefault(g => g.Name == gaugeName);
        }
    }
}
