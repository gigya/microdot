using System;
using System.Diagnostics;
using System.Threading;

namespace Metrics.PerfCounters
{
    internal static class ThreadPoolMetrics
    {
        internal static void RegisterThreadPoolGauges(MetricsContext context)
        {
            context.Gauge("Thread Pool Available Threads", () => { int threads, ports; ThreadPool.GetAvailableThreads(out threads, out ports); return threads; }, Unit.Threads, tags: "threads");
            context.Gauge("Thread Pool Available Completion Ports", () => { int threads, ports; ThreadPool.GetAvailableThreads(out threads, out ports); return ports; }, Unit.Custom("Ports"), tags: "threads");

            context.Gauge("Thread Pool Min Threads", () => { int threads, ports; ThreadPool.GetMinThreads(out threads, out ports); return threads; }, Unit.Threads, tags: "threads");
            context.Gauge("Thread Pool Min Completion Ports", () => { int threads, ports; ThreadPool.GetMinThreads(out threads, out ports); return ports; }, Unit.Custom("Ports"), tags: "threads");

            context.Gauge("Thread Pool Max Threads", () => { int threads, ports; ThreadPool.GetMaxThreads(out threads, out ports); return threads; }, Unit.Threads, tags: "threads");
            context.Gauge("Thread Pool Max Completion Ports", () => { int threads, ports; ThreadPool.GetMaxThreads(out threads, out ports); return ports; }, Unit.Custom("Ports"), tags: "threads");

            var currentProcess = Process.GetCurrentProcess();
            Func<TimeSpan> uptime = () => (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime());
            context.Gauge(currentProcess.ProcessName + " Uptime Seconds", () => uptime().TotalSeconds, Unit.Custom("Seconds"));
            context.Gauge(currentProcess.ProcessName + " Uptime Hours", () => uptime().TotalHours, Unit.Custom("Hours"));
            context.Gauge(currentProcess.ProcessName + " Threads", () => Process.GetCurrentProcess().Threads.Count, Unit.Threads, tags: "threads");
        }
    }
}
