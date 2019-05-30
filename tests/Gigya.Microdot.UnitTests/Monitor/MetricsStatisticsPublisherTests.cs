using System.Collections.Generic;
using System.Linq;
using System.Net;

using Gigya.Microdot.Orleans.Hosting;

using Metrics;
using Metrics.MetricData;

using NSubstitute;

using NUnit.Framework;

using Orleans.Providers;
using Orleans.Runtime;
using Shouldly;

namespace Gigya.Microdot.UnitTests
{
    /* //#ORLEANS20
    [TestFixture]
    public class MetricsStatisticsPublisherTests
    {
        MetricsStatisticsPublisher publisher;
        Logger logger; 
    
        [SetUp]
        public void PublishStatistics()
        {
            Metric.ShutdownContext("Silo");  
            publisher = new MetricsStatisticsPublisher();
            var metricsName = publisher.GetType().Name;

            logger = Substitute.For<Logger>();
            logger.SeverityLevel.Returns(Severity.Info);
            
            var providerRuntime = Substitute.For<IProviderRuntime>();
            providerRuntime.GetLogger(metricsName).ReturnsForAnyArgs(logger);

            var providerConfiguration = Substitute.For<IProviderConfiguration, IConfigurableSiloMetricsDataPublisher, IConfigurableStatisticsPublisher>();
            publisher.Init(metricsName, providerRuntime, providerConfiguration);
        }

        [Test]
        public void EmptyMethodsNotCrashTest()
        {
            publisher.AddConfiguration("", "", "", IPAddress.Loopback);            
            ((IConfigurableSiloMetricsDataPublisher) publisher).AddConfiguration("", true, "", null, null, "");
            ((IConfigurableStatisticsPublisher) publisher).AddConfiguration("", true, "", null, null, "");            
            publisher.Init(true, "", "", "", "", "");
            publisher.Init("", "", null, "", null, "");
            publisher.Init(null, IPAddress.Loopback, "");
            publisher.ReportMetrics( Substitute.For<ISiloPerformanceMetrics>());
            publisher.ReportMetrics(Substitute.For<IClientPerformanceMetrics>());
        }

        [Test]
        public void ClosePublisherTest()
        {
            publisher.ReportStats(null).Wait();
            publisher.Close();            
        }

        [Test]
        public void PublishEmptyListTest()
        {
            publisher.ReportStats(new List<ICounter>()).Wait();
            GetMetricsData().Gauges.ShouldNotBeNull();
        }

        [Test]
        public void PublishNullTest()
        {
            publisher.ReportStats(null).Wait();
            GetMetricsData().Gauges.ShouldNotBeNull();
        }

        [Test]
        public void LogOnlyCounterNotPublishedTest()
        {
            var counter = Substitute.For<ICounter>();
            counter.Storage.Returns(CounterStorage.LogOnly);
            counter.IsValueDelta.Returns(false);
            counter.Name.Returns("LogOnlyCounter");
            counter.GetValueString().Returns("BlaBla");
            
            publisher.ReportStats(new List<ICounter> { counter }).Wait();

            GetGauge("LogOnlyCounter").ShouldBeNull();
        }

        [Test]
        public void PublishNonDeltaCounterTest()
        {
            var counter = Substitute.For<ICounter>();
            counter.Storage.Returns(CounterStorage.LogAndTable);
            counter.IsValueDelta.Returns(false);
            counter.Name.Returns("NonDeltaCounter");
            counter.GetValueString().Returns("100");

            publisher.ReportStats(new List<ICounter> { counter }).Wait();
            
            GetGauge("NonDeltaCounter").Value.ShouldBe(100);
        }
        [Test]
        public void UpdateNonDeltaCounterTest()
        {
            var counter = Substitute.For<ICounter>();
            counter.Storage.Returns(CounterStorage.LogAndTable);
            counter.IsValueDelta.Returns(false);
            counter.Name.Returns("NonDeltaCounter");
            counter.GetValueString().Returns("100");
            
            publisher.ReportStats(new List<ICounter> { counter }).Wait();
            
            GetGauge("NonDeltaCounter").Value.ShouldBe(100);

            counter.GetValueString().Returns("300");
            publisher.ReportStats(new List<ICounter> { counter }).Wait();

            GetGauge("NonDeltaCounter").Value.ShouldBe(300);
        }

        [Test]
        public void PublishDeltaCounterTest()
        { 
            var counter = Substitute.For<ICounter>();
            counter.Storage.Returns(CounterStorage.LogAndTable);
            counter.IsValueDelta.Returns(true);
            counter.Name.Returns("DeltaCounter");
            counter.GetDeltaString().Returns("100");

            publisher.ReportStats(new List<ICounter> { counter }).Wait();
           
            GetGauge("DeltaCounter").Value.ShouldBe(100);
        }

        [Test]
        public void UpdateDeltaCounterTest()
        {
            var counter = Substitute.For<ICounter>();
            counter.Storage.Returns(CounterStorage.LogAndTable);
            counter.IsValueDelta.Returns(true);
            counter.Name.Returns("DeltaCounter");
            counter.GetDeltaString().Returns("100");

            publisher.ReportStats(new List<ICounter> { counter }).Wait();

            GetGauge("DeltaCounter").Value.ShouldBe(100);

            publisher.ReportStats(new List<ICounter> { counter }).Wait();

            GetGauge("DeltaCounter").Value.ShouldBe(200);
        }

        private static MetricsDataEquatable GetMetricsRepresentation(string counterName,long val)
        {
            return new MetricsDataEquatable
            {
                GaugesSettings = new MetricsCheckSetting {CheckValues = true},
                Gauges = new List<MetricDataEquatable>
                {
                    new MetricDataEquatable {Name = counterName, Value = val, Unit = Unit.None}
                }
            };
        }

        private static MetricsData GetMetricsData()
        {
            return
                Metric.Context("Silo")                    
                    .DataProvider.CurrentMetricsData;
        }

        private static GaugeValueSource GetGauge(string gaugeName)
        {
            return GetMetricsData().Gauges.FirstOrDefault(g => g.Name == gaugeName);
        }

    }
    */
}
