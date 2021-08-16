using System;
using System.Diagnostics;
using System.IO;

namespace Metrics.EventCounters.Linux.CPU
{
    public class ProcessInfo
    {
        public ProcessInfo()
        {
            using (var process = Process.GetCurrentProcess())
            {
                var processTimes = CpuHelper.GetProcessTimes(process);
                TotalProcessorTimeTicks = processTimes.TotalProcessorTimeTicks;
                TimeTicks = processTimes.TimeTicks;

                ActiveCores = CpuHelper.GetNumberOfActiveCores(process);
            }
        }

        public long TotalProcessorTimeTicks { get; }

        public long TimeTicks { get; }

        public long ActiveCores { get; }
    }

    public class SystemTime
    {
        private static readonly SystemTime Instance = new SystemTime();

        public Func<DateTime> UtcDateTime;

        // public Action<int> WaitCalled;

        public DateTime GetUtcNow()
        {
            var temp = UtcDateTime;
            return temp?.Invoke() ?? DateTime.UtcNow;
        }

        public static DateTime UtcNow => Instance.GetUtcNow();
    }
}
