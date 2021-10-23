using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Metrics.EventCounters.CPU;
using System;
using System.Diagnostics;
using System.Linq;
using Timer = System.Threading.Timer;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public sealed class WorkloadMetrics : IWorkloadMetrics
    {
        private readonly AggregatingHealthStatus _healthStatus;
        private readonly Func<WorkloadMetricsConfig> _getConfig;
        private readonly IDateTime _dateTime;
        private readonly PerformanceEventListener _eventListener;

        private LowSensitivityHealthCheck _cpuUsageHealthCheck;
        private LowSensitivityHealthCheck _threadsCountHealthCheck;
        private LowSensitivityHealthCheck _orleansQueueHealthCheck;
        private ICpuUsageCalculator _cpuUsageCalculator;

        private readonly MetricsContext _context = Metric.Context("Workload");
        private Timer _triggerHealthChecksEvery5Seconds;
        private bool _disposed;

        private ILog Log { get; }


        public WorkloadMetrics(Func<string, AggregatingHealthStatus> getAggregatingHealthStatus, Func<WorkloadMetricsConfig> getConfig, IDateTime dateTime, ILog log, PerformanceEventListener eventListener)
        {
            Log = log;
            _getConfig = getConfig;
            _dateTime = dateTime;
            _eventListener = eventListener;
            _cpuUsageCalculator = CpuHelper.GetOSCpuUsageCalculator();
            _healthStatus = getAggregatingHealthStatus("Workload");
        }

        
        public void Init()
        {
            _eventListener.Subscribe("% Processor Time");
            _eventListener.Subscribe("# of current logical Threads");
            _eventListener.Subscribe("threadpool-queue-length");
            _eventListener.Subscribe("threadpool-completed-items-count");
            _eventListener.Subscribe("working-set");
            _eventListener.Subscribe("# Bytes in all Heaps");
            _eventListener.Subscribe("Allocated Bytes/second");
            _eventListener.Subscribe("POH Size");
            _eventListener.Subscribe("LOH Size");
            _eventListener.Subscribe("# Gen 0 Collections");
            _eventListener.Subscribe("# Gen 1 Collections"); 
            _eventListener.Subscribe("# Gen 2 Collections");
            _eventListener.Subscribe("Gen 0 heap size");
            _eventListener.Subscribe("Gen 1 heap size");
            _eventListener.Subscribe("Gen 2 heap size");
            _eventListener.Subscribe("% Time in GC");
            _eventListener.Subscribe("gc-fragmentation");
            _eventListener.Subscribe("# of Exceps Thrown / Sec");
            _eventListener.Subscribe("active-timer-count");
                       
            _context.Context("CPU").Gauge("Machine Cpu Usage", () => _cpuUsageCalculator.Calculate().MachineCpuUsage, Unit.Percent);          
            _context.Context("CPU").Gauge("Processor Affinity", () => Process.GetCurrentProcess().ProcessorAffinityList().Count(), Unit.Items);
            _context.Context("CPU").Gauge("CPU usage", () => ReadPerfCounter("% Processor Time"), Unit.Percent);
            _context.Context("ThreadPool").Gauge("Thread Count", () => { double threads = ReadPerfCounter("# of current logical Threads"); return threads < 0 || threads > 1000000 ? 0 : threads; }, Unit.Items);
            _context.Context("ThreadPool").Gauge("Queue Length", () => { double threads = ReadPerfCounter("threadpool-queue-length"); return threads < 0 || threads > 1000000 ? 0 : threads; }, Unit.Items);
            _context.Context("ThreadPool").Gauge("Completed Item Count", () => { double threads = ReadPerfCounter("threadpool-completed-items-count"); return threads < 0 || threads > 1000000 ? 0 : threads; }, Unit.Items);
            _context.Context("Memory").Gauge("Working set", () => ReadPerfCounter("working-set"), Unit.Bytes); 
            _context.Context("Memory").Gauge("Bytes in all Heaps", () => ReadPerfCounter("# Bytes in all Heaps"), Unit.Bytes);
            _context.Context("Memory").Gauge("Allocated Bytes/second", () => ReadPerfCounter("Allocated Bytes/second"), Unit.Bytes);
            _context.Context("Memory").Gauge("POH Size", () => ReadPerfCounter("poh-size"), Unit.Bytes);
            _context.Context("Memory").Gauge("LOH Size", () => ReadPerfCounter("loh-size"), Unit.Bytes);
            _context.Context("GC").Gauge("Gen-0 collections", () => ReadPerfCounter("# Gen 0 Collections"), Unit.Items);
            _context.Context("GC").Gauge("Gen-1 collections", () => ReadPerfCounter("# Gen 1 Collections"), Unit.Items);
            _context.Context("GC").Gauge("Gen-2 collections", () => ReadPerfCounter("# Gen 2 Collections"), Unit.Items);
            _context.Context("GC").Gauge("Gen 0 heap size", () => ReadPerfCounter("Gen 0 heap size"), Unit.Bytes);
            _context.Context("GC").Gauge("Gen 1 heap size", () => ReadPerfCounter("Gen 1 heap size"), Unit.Bytes);
            _context.Context("GC").Gauge("Gen 2 heap size", () => ReadPerfCounter("Gen 2 heap size"), Unit.Bytes);
            _context.Context("GC").Gauge("Time in GC", () => ReadPerfCounter("% Time in GC"), Unit.Percent);
            _context.Context("GC").Gauge("GC Fragmentation", () => ReadPerfCounter("gc-fragmentation"), Unit.Percent);            
            _context.Context("General").Gauge("Exceps Thrown / Sec", () => ReadPerfCounter("# of Exceps Thrown / Sec"), Unit.Items);
            _context.Context("General").Gauge("Active Timers", () => ReadPerfCounter("active-timer-count"), Unit.Items);


            _cpuUsageHealthCheck = new LowSensitivityHealthCheck(CpuUsageHealth, () => _getConfig().MinUnhealthyDuration, _dateTime);
            _threadsCountHealthCheck = new LowSensitivityHealthCheck(ThreadsCountHealth, () => _getConfig().MinUnhealthyDuration, _dateTime);
            _orleansQueueHealthCheck = new LowSensitivityHealthCheck(OrleansRequestQueueHealth, () => _getConfig().MinUnhealthyDuration, _dateTime);

            _healthStatus.RegisterCheck("CPU Usage", _cpuUsageHealthCheck.GetHealthStatus);
            _healthStatus.RegisterCheck("Threads Count", _threadsCountHealthCheck.GetHealthStatus);
            _healthStatus.RegisterCheck("Orleans Queue", _orleansQueueHealthCheck.GetHealthStatus);

            _triggerHealthChecksEvery5Seconds = new Timer(TriggerHealthCheck, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private double ReadPerfCounter(string performanceCounterName)
        {
            if (_getConfig().ReadPerformanceCounters)
            {
                return _eventListener.ReadPerfCounter(performanceCounterName) ?? 0;
            }
            
            return 0;
        }

        private void TriggerHealthCheck(object state)
        {
            try
            {
                _cpuUsageHealthCheck.GetHealthStatus();
                _threadsCountHealthCheck.GetHealthStatus();
            }
            catch (Exception ex)
            {
                Log.Warn(x => x("Error triggering workload health status", exception: ex));
            }
        }

        private HealthCheckResult CpuUsageHealth()
        {
            if (!_getConfig().ReadPerformanceCounters)
                return HealthCheckResult.Healthy("CPU Usage: Reading perf counter disabled by configuration");

            var cpuUsage = ReadPerfCounter("% Processor Time");
            var maxCpuUsage = _getConfig().MaxHealthyCpuUsage;

            if (cpuUsage > maxCpuUsage)
                return HealthCheckResult.Unhealthy($"CPU Usage: {cpuUsage}% (unhealthy above {maxCpuUsage}%)");
            return HealthCheckResult.Healthy($"CPU Usage: {cpuUsage}% (unhealthy above {maxCpuUsage}%)");
        }


        private HealthCheckResult ThreadsCountHealth()
        {
            if (!_getConfig().ReadPerformanceCounters)
                return HealthCheckResult.Healthy("Threads: Reading perf counter disabled by configuration");

            var threads = ReadPerfCounter("# of current logical Threads");
            var maxThreads = _getConfig().MaxHealthyThreadsCount;

            if (threads > maxThreads)
                return HealthCheckResult.Unhealthy($"Threads: {threads} (unhealthy above {maxThreads})");
            return HealthCheckResult.Healthy($"Threads: {threads} (unhealthy above {maxThreads})");
        }


        private HealthCheckResult OrleansRequestQueueHealth()
        {
            var queueLength = Metric.Context("Silo").DataProvider.CurrentMetricsData.Gauges
                                                    .FirstOrDefault(x => x.Name == "Request queue length")?.Value;
            if (queueLength == null)
                return HealthCheckResult.Healthy("Orleans queue length: unknown");

            var maxLength = _getConfig().MaxHealthyOrleansQueueLength;
            if (queueLength > maxLength)
                return HealthCheckResult.Unhealthy($"Orleans queue length: {queueLength} (unhealthy above {maxLength})");

            return HealthCheckResult.Healthy($"Orleans queue length: {queueLength} (unhealthy above {maxLength})");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _context?.Dispose();
            _cpuUsageCalculator?.Dispose();
            _triggerHealthChecksEvery5Seconds?.Dispose();
        }

    }
}

