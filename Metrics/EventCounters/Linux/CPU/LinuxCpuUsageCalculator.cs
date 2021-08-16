using System;
using System.IO;

namespace Metrics.EventCounters.Linux.CPU
{

    public class LinuxCpuUsageCalculator : CpuUsageCalculator<LinuxInfo>
    {
        private readonly char[] _separators = { ' ', '\t' };

        public override (double MachineCpuUsage, double? MachineIoWait) CalculateMachineCpuUsage(LinuxInfo linuxInfo)
        {
            double machineCpuUsage = 0;
            double? machineIoWait = 0;
            if (linuxInfo.TotalIdle >= PreviousInfo.TotalIdle &&
                linuxInfo.TotalWorkTime >= PreviousInfo.TotalWorkTime)
            {
                var idleDiff = linuxInfo.TotalIdle - PreviousInfo.TotalIdle;
                var workDiff = linuxInfo.TotalWorkTime - PreviousInfo.TotalWorkTime;
                var totalSystemWork = idleDiff + workDiff;
                var ioWaitDiff = linuxInfo.TotalIoWait - PreviousInfo.TotalIoWait;

                if (totalSystemWork > 0)
                {
                    machineCpuUsage = (workDiff * 100.0) / totalSystemWork;
                    machineIoWait = (ioWaitDiff * 100.0) / totalSystemWork;
                }
            }
            else if (LastCpuUsage != null)
            {
                // overflow
                machineCpuUsage = LastCpuUsage.Value.MachineCpuUsage;
                machineIoWait = LastCpuUsage.Value.MachineIoWait;
            }

            return (machineCpuUsage, machineIoWait);
        }

        public override LinuxInfo GetProcessInfo()
        {
            var lines = File.ReadLines("/proc/stat");
            foreach (var line in lines)
            {
                if (line.StartsWith("cpu", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                var items = line.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
                if (items.Length == 0 || items.Length < 9)
                    continue;

                return new LinuxInfo
                {
                    TotalUserTime = ulong.Parse(items[1]),
                    TotalUserLowTime = ulong.Parse(items[2]),
                    TotalSystemTime = ulong.Parse(items[3]),
                    TotalIdleTime = ulong.Parse(items[4]),
                    TotalIoWait = ulong.Parse(items[5]),
                    TotalIRQTime = ulong.Parse(items[6]),
                    TotalSoftIRQTime = ulong.Parse(items[7]),
                    TotalStealTime = ulong.Parse(items[8])
                };
            }

            return null;
        }
    }
}
