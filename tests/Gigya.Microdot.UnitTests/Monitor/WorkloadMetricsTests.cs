﻿//using Gigya.Microdot.Fakes;
//using Gigya.Microdot.Interfaces.SystemWrappers;
//using Gigya.Microdot.SharedLogic;
//using Gigya.Microdot.SharedLogic.Measurement.Workload;
//using Gigya.Microdot.SharedLogic.Monitor;
//using Gigya.Microdot.Testing.Shared;
//using Metrics;
//using Metrics.Core;
//using Metrics.MetricData;
//using Ninject;
//using NUnit.Framework;
//using Shouldly;
//using System;
//using System.Diagnostics;
//using System.Linq;
//using System.Threading.Tasks;

//namespace Gigya.Microdot.UnitTests.Monitor
//{
//    [TestFixture, Parallelizable(ParallelScope.None)]
//    public class WorkloadMetricsTests
//    {
//        private const string Cpu = "CPU";
//        private const string ThreadPool = "ThreadPool";
//        private const string Memory = "Memory";
//        private const string Gc = "GC";

//        private const string CpuUsage = "CPU usage";
//        private const string CpuTotal = "Machine Cpu Usage";
//        private const string GcGen2Collections = "Gen-2 collections";
//        private const string TimeInGc = "Time in GC";
//        private const string MemoryWorkingSet = "Working set";
//        private const string ThreadCount = "Thread Count";

//        private IKernel _kernel;
//        private ServiceArguments _serviceArguments;
//        private WorkloadMetricsConfig _config;
//        private DateTimeFake _dateTimeFake;
//        private int _orleansQueueLength;
//        private IWorkloadMetrics _workloadMetrics;

//        private readonly TimeSpan MinUnhealthyDuration = TimeSpan.FromMinutes(1);

//        [SetUp]
//        public void Setup()
//        {
//            Metric.ShutdownContext("Workload");
//            Metric.ShutdownContext("Silo");
//            _serviceArguments = new ServiceArguments();
//            _dateTimeFake = new DateTimeFake { UtcNow = DateTime.UtcNow };

//            _kernel = new TestingKernel<ConsoleLog>(k =>
//            {
//                k.Rebind<IHealthMonitor>().To<HealthMonitor>();
//                k.Rebind<ServiceArguments>().ToMethod(c => _serviceArguments);
//                k.Rebind<IDateTime>().ToMethod(c => _dateTimeFake);

//                k.Bind<PerformanceEventListener>().To<PerformanceEventListener>().InSingletonScope();
//            });

//            _kernel.Get<Ninject.SystemInitializer.SystemInitializer>().Init();
//            _config = _kernel.Get<Func<WorkloadMetricsConfig>>()();
//            _config.ReadPerformanceCounters = true;
//            _config.MinUnhealthyDuration = MinUnhealthyDuration;

//            SetupOrleansQueueLength();

//            _workloadMetrics = _kernel.Get<IWorkloadMetrics>();
//        }

//        [TearDown]
//        public void TearDown()
//        {
//            _kernel?.Dispose();
//        }

//        private void SetupOrleansQueueLength()
//        {
//            _orleansQueueLength = 1;
//            Metric.Context("Silo").Gauge("Request queue length", () => _orleansQueueLength, Unit.None);
//        }


//        private const int Repeat = 1;

//        [Test]
//        [Repeat(Repeat)]
//        public async Task AddWorkloadGaugesToMetrics()
//        {
//            Init();
//            AssertMetricIsPositive(Cpu, CpuUsage);
//            AssertMetricIsPositive(Cpu, CpuTotal);
//            AssertMetricIsPositive(Gc, GcGen2Collections);
//            AssertMetricIsPositive(Gc, TimeInGc);
//            AssertMetricIsPositive(Memory, MemoryWorkingSet);
//            AssertMetricIsPositive(ThreadPool, ThreadCount);
//        }


//        [Test]
//        [Repeat(Repeat)]
//        public async Task MetricsShouldBeZeroIfConfiguredNotToReadPerformanceCounters()
//        {
//            _config.ReadPerformanceCounters = false;
//            Init();
//            AssertMetricIsZero(Cpu, CpuUsage);
//            AssertMetricIsZero(Gc, GcGen2Collections);
//            AssertMetricIsZero(Gc, TimeInGc);
//            AssertMetricIsZero(Memory, MemoryWorkingSet);
//            AssertMetricIsZero(ThreadPool, ThreadCount);
//        }


//        [Test]
//        [Repeat(Repeat)]
//        public async Task AddWorkloadHealthCheck()
//        {
//            Init();
//            GetHealthCheck().IsHealthy.ShouldBeTrue();
//        }

//        [Test]
//        [Repeat(Repeat)]
//        public async Task BeUnhealthyAfterThreadsCountIsTooHighForMoreThanSpecifiedDuration()
//        {
//            _config.MaxHealthyThreadsCount = 1;
//            Init();
//            await Task.Delay(1000);
//            _dateTimeFake.UtcNow += MinUnhealthyDuration + TimeSpan.FromSeconds(1);
//            GetHealthCheck().IsHealthy.ShouldBe(false);
//        }

//        [Test]
//        [Repeat(Repeat)]
//        public async Task BeUnhealthyAfterCPUUsageIsTooHighForMoreThanSpecifiedDuration()
//        {
//            _config.MaxHealthyCpuUsage = 0.01;
//            Init();

//            Task.Run(async () =>
//            {
//                var random = new Random();
//                while (true)
//                {
//                    random.Next(10000000);
//                }
//            });
//            await Task.Delay(2000);
//            _dateTimeFake.UtcNow += MinUnhealthyDuration + TimeSpan.FromSeconds(0.1);
//            GetHealthCheck().IsHealthy.ShouldBe(false);
//        }

//        [Test]
//        [Repeat(Repeat)]
//        public async Task BeUnhealthyAfterOrleansQueueIsTooHighForMoreThanSpecifiedDuration()
//        {
//            _config.MaxHealthyOrleansQueueLength = 1;
//            Init();
//            _orleansQueueLength = 5;
//            _dateTimeFake.UtcNow += MinUnhealthyDuration + TimeSpan.FromSeconds(1);
//            GetHealthCheck().IsHealthy.ShouldBe(false);
//        }

//        [Test]
//        [Repeat(Repeat)]
//        public async Task BeHealthyIfProblemDetectedForLessThanSpecifiedDuration()
//        {
//            _config.MaxHealthyThreadsCount = 1;
//            Init();
//            _dateTimeFake.UtcNow += MinUnhealthyDuration - TimeSpan.FromSeconds(1);
//            GetHealthCheck().IsHealthy.ShouldBe(true);
//        }

//        [Test]
//        [Repeat(Repeat)]
//        public async Task BeHealthyIfProblemWasSolvedDuringSpecifiedDuration()
//        {
//            _config.MaxHealthyThreadsCount = 1;
//            // problem is detected 
//            Init();

//            // problem is solved before duration end
//            _dateTimeFake.UtcNow += MinUnhealthyDuration - TimeSpan.FromSeconds(1);
//            _config.MaxHealthyThreadsCount = 1000;
//            GetHealthCheck();

//            // Duration has passed. Problem is detected again
//            _dateTimeFake.UtcNow += TimeSpan.FromSeconds(5);
//            _config.MaxHealthyThreadsCount = 1;

//            // Service is healthy, because it detected that problem was solved during the specified duration
//            GetHealthCheck().IsHealthy.ShouldBe(true);
//        }

//        [Test]
//        public void AffinityCoresCount()
//        {
//            var p = Process.GetCurrentProcess().ProcessorAffinityList().Count();
//            p.ShouldBeInRange(1, Environment.ProcessorCount);
//        }

//        [Test]
//        public void AffinityCoresIteration()
//        {
//            var p = Process.GetCurrentProcess();
//            p.ProcessorAffinityList().ToList().Count.ShouldBeInRange(1, Environment.ProcessorCount);
//            p.ProcessorAffinityList().ToList().ForEach(index => index.ShouldBeInRange(0, Environment.ProcessorCount - 1));
//        }

//        private GaugeValueSource MetricShouldExist(string context, string gaugeName)
//        {
//            var gauge = GetGauge(context, gaugeName);
//            gauge.ShouldNotBeNull($"Gauge '{gaugeName}' does not exist");
//            return gauge;
//        }


//        private void AssertMetricIsPositive(string context, string gaugeName)
//        {
//            MetricShouldExist(context, gaugeName)?
//                .Value.ShouldBeGreaterThanOrEqualTo(0.00, $"Gauge '{gaugeName}' should have positive value");
//        }


//        private void AssertMetricIsZero(string context, string gaugeName)
//        {
//            MetricShouldExist(context, gaugeName)?
//                .Value.ShouldBe(0, $"Gauge '{gaugeName}' should be with value zero");
//        }




//        public void Init()
//        {
//            _workloadMetrics.Init();
//            HealthChecks.GetStatus();
//        }


//        private static GaugeValueSource GetGauge(string context, string gaugeName)
//        {
//            return Metric.Context("Workload").Context(context).DataProvider.
//                          CurrentMetricsData.Gauges.FirstOrDefault(x => x.Name == gaugeName);
//        }


//        private HealthCheckResult GetHealthCheck()
//        {
//            HealthCheck.Result result = HealthChecks.GetStatus().Results.First(r => r.Name == "Workload");
//            Console.WriteLine(result.Check.IsHealthy);
//            Console.WriteLine(result.Check.Message);
//            return result.Check;
//        }


//    }
//}
