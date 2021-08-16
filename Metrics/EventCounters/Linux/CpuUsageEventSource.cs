using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Metrics.EventCounters.Linux
{
    [EventSource(Name = "Gigya.EventCounters")]
    public sealed class CpuUsageEventSource : EventSource
    {
        public readonly static CpuUsageEventSource EventSource = new CpuUsageEventSource();
        ICpuUsageCalculator calculator;
        private readonly ConcurrentDictionary<string, EventCounter> _dynamicCounters = new ConcurrentDictionary<string, EventCounter>();
        private readonly Timer _cpuUsageEventSourceTime;
        private  MetricsContext Metrics { get; }
     
        private CpuUsageEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
        {
            _cpuUsageEventSourceTime = Metrics.Timer("CpuUsageEventSource", Unit.Calls);
            calculator = new LinuxCpuUsageCalculator();
            calculator.Init();

            using (_cpuUsageEventSourceTime.NewContext())
            {
                var metrics = calculator.Calculate();
                RecordMetric("MachineCpuUsage", metrics.MachineCpuUsage);
                RecordMetric("ProcessCpuUsage", metrics.ProcessCpuUsage);
            }
        }


        public void RecordMetric(string name, float value)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var counter = _dynamicCounters.GetOrAdd(name, key => new EventCounter(key, this));
            counter.WriteMetric(value);

        }



        //public void CountRoute1()
        //{
        //    //Interlocked.Increment(ref _routeCounter1);
        //}




        /////
        //private static char[] _separators = { ' ', '\t' };

        //private (double MachineCpuUsage, double? MachineIoWait) CalculateMachineCpuUsage(LinuxInfo linuxInfo)
        //{
        //    double machineCpuUsage = 0;
        //    double? machineIoWait = 0;
        //    if (linuxInfo.TotalIdle >= PreviousInfo.TotalIdle &&
        //        linuxInfo.TotalWorkTime >= PreviousInfo.TotalWorkTime)
        //    {
        //        var idleDiff = linuxInfo.TotalIdle - PreviousInfo.TotalIdle;
        //        var workDiff = linuxInfo.TotalWorkTime - PreviousInfo.TotalWorkTime;
        //        var totalSystemWork = idleDiff + workDiff;
        //        var ioWaitDiff = linuxInfo.TotalIoWait - PreviousInfo.TotalIoWait;

        //        if (totalSystemWork > 0)
        //        {
        //            machineCpuUsage = (workDiff * 100.0) / totalSystemWork;
        //            machineIoWait = (ioWaitDiff * 100.0) / totalSystemWork;
        //        }
        //    }
        //    else if (LastCpuUsage != null)
        //    {
        //        // overflow
        //        machineCpuUsage = LastCpuUsage.Value.MachineCpuUsage;
        //        machineIoWait = LastCpuUsage.Value.MachineIoWait;
        //    }

        //    return (machineCpuUsage, machineIoWait);
        //}

        //internal static class RuntimeEventSourceHelper
        //{
        //   // private static Interop.Sys.ProcessCpuInformation s_cpuInfo;

        //    internal static int GetCpuUsage() =>
        //        Interop.Sys.GetCpuUtilization(ref s_cpuInfo) / Environment.ProcessorCount;
        //}
        //private  LinuxInfo GetProcessInfo()
        //{
        //    var lines = File.ReadLines("/proc/stat");
        //    foreach (var line in lines)
        //    {
        //        if (line.StartsWith("cpu", StringComparison.OrdinalIgnoreCase) == false)
        //            continue;

        //        var items = line.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
        //        if (items.Length == 0 || items.Length < 9)
        //            continue;

        //        return new LinuxInfo
        //        {
        //            TotalUserTime = ulong.Parse(items[1]),
        //            TotalUserLowTime = ulong.Parse(items[2]),
        //            TotalSystemTime = ulong.Parse(items[3]),
        //            TotalIdleTime = ulong.Parse(items[4]),
        //            TotalIoWait = ulong.Parse(items[5]),
        //            TotalIRQTime = ulong.Parse(items[6]),
        //            TotalSoftIRQTime = ulong.Parse(items[7]),
        //            TotalStealTime = ulong.Parse(items[8])
        //        };
        //    }

        //    return null;
        //}


        //internal class ProcessInfo
        //{
        //    protected ProcessInfo()
        //    {
        //        using (var process = Process.GetCurrentProcess())
        //        {
        //            var processTimes = CpuHelper.GetProcessTimes(process);
        //            TotalProcessorTimeTicks = processTimes.TotalProcessorTimeTicks;
        //            TimeTicks = processTimes.TimeTicks;

        //            ActiveCores = CpuHelper.GetNumberOfActiveCores(process);
        //        }
        //    }

        //    public long TotalProcessorTimeTicks { get; }

        //    public long TimeTicks { get; }

        //    public long ActiveCores { get; }
        //}
        //internal class LinuxInfo : ProcessInfo
        //{
        //    public ulong TotalUserTime { private get; set; }

        //    public ulong TotalUserLowTime { private get; set; }

        //    public ulong TotalSystemTime { private get; set; }

        //    public ulong TotalIdleTime { private get; set; }

        //    public ulong TotalIoWait { get; set; }

        //    public ulong TotalIRQTime { private get; set; }

        //    public ulong TotalSoftIRQTime { private get; set; }

        //    public ulong TotalStealTime { private get; set; }

        //    public ulong TotalWorkTime => TotalUserTime + TotalUserLowTime + TotalSystemTime +
        //                                  TotalIRQTime + TotalSoftIRQTime + TotalStealTime;

        //    public ulong TotalIdle => TotalIdleTime + TotalIoWait;
        //}

        //public static class CpuHelper
        //{
        //  //  private static readonly Logger Logger = LoggingSource.Instance.GetLogger<MachineResources>("Server");

        //    //internal static ICpuUsageCalculator GetOSCpuUsageCalculator()
        //    //{
        //    //    ICpuUsageCalculator calculator;
        //    //    if (PlatformDetails.RunningOnPosix == false)
        //    //    {
        //    //        calculator = new WindowsCpuUsageCalculator();
        //    //    }
        //    //    else if (PlatformDetails.RunningOnMacOsx)
        //    //    {
        //    //        calculator = new MacInfoCpuUsageCalculator();
        //    //    }
        //    //    else
        //    //    {
        //    //        calculator = new LinuxCpuUsageCalculator();
        //    //    }
        //    //    calculator.Init();
        //    //    return calculator;
        //    //}

        //    //internal static ExtensionPointCpuUsageCalculator GetExtensionPointCpuUsageCalculator(
        //    //    JsonContextPool contextPool,
        //    //    MonitoringConfiguration configuration,
        //    //    NotificationCenter.NotificationCenter notificationCenter)
        //    //{
        //    //    var extensionPoint = new ExtensionPointCpuUsageCalculator(
        //    //        contextPool,
        //    //        configuration.CpuUsageMonitorExec,
        //    //        configuration.CpuUsageMonitorExecArguments,
        //    //        notificationCenter);



        //    //    return extensionPoint;
        //    //}

        //    public static long GetNumberOfActiveCores(Process process)
        //    {
        //        try
        //        {
        //            return Bits.NumberOfSetBits(process.ProcessorAffinity.ToInt64());
        //        }
        //        catch (NotSupportedException)
        //        {
        //            return ProcessorInfo.ProcessorCount;
        //        }
        //        catch (Exception e)
        //        {
        //            //if (Logger.IsInfoEnabled)
        //            //    Logger.Info("Failure to get the number of active cores", e);

        //            return ProcessorInfo.ProcessorCount;
        //        }
        //    }

        //    public static (long TotalProcessorTimeTicks, long TimeTicks) GetProcessTimes(Process process)
        //    {
        //        try
        //        {
        //            var timeTicks = SystemTime.UtcNow.Ticks;
        //            var totalProcessorTime = process.TotalProcessorTime.Ticks;
        //            return (TotalProcessorTimeTicks: totalProcessorTime, TimeTicks: timeTicks);
        //        }
        //        catch (NotSupportedException)
        //        {
        //            return (0, 0);
        //        }
        //        catch (Exception e)
        //        {
        //            //if (Logger.IsInfoEnabled)
        //            //    Logger.Info($"Failure to get process times, error: {e.Message}", e);

        //            return (0, 0);
        //        }
        //    }
        //}
        //public class SystemTime
        //{
        //    private static readonly SystemTime Instance = new SystemTime();

        //    /// <summary>
        //    /// Tests now run in parallel so this is no longer static to mitigate the possibility of getting incorrect results. Use DocumentDatabase.Time instead.
        //    /// </summary>
        //    public Func<DateTime> UtcDateTime;

        //    public Action<int> WaitCalled;

        //    public DateTime GetUtcNow()
        //    {
        //        var temp = UtcDateTime;
        //        return temp?.Invoke() ?? DateTime.UtcNow;
        //    }

        //    public static DateTime UtcNow => Instance.GetUtcNow();
        //}



    }
}
