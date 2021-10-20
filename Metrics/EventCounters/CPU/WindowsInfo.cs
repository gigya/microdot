
namespace Metrics.EventCounters.CPU
{
    public class WindowsInfo : ProcessInfo
    {
        public ulong SystemIdleTime { get; set; }

        public ulong SystemKernelTime { get; set; }

        public ulong SystemUserTime { get; set; }
    }
}
