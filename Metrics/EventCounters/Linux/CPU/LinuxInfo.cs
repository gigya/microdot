
namespace Metrics.EventCounters.Linux.CPU
{
    public class LinuxInfo : ProcessInfo
    {
        public ulong TotalUserTime { private get; set; }

        public ulong TotalUserLowTime { private get; set; }

        public ulong TotalSystemTime { private get; set; }

        public ulong TotalIdleTime { private get; set; }

        public ulong TotalIoWait { get; set; }

        public ulong TotalIRQTime { private get; set; }

        public ulong TotalSoftIRQTime { private get; set; }

        public ulong TotalStealTime { private get; set; }

        public ulong TotalWorkTime => TotalUserTime + TotalUserLowTime + TotalSystemTime +
                                      TotalIRQTime + TotalSoftIRQTime + TotalStealTime;

        public ulong TotalIdle => TotalIdleTime + TotalIoWait;
    }

}
