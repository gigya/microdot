using System;

namespace Metrics.EventCounters.CPU
{
    public interface ICpuUsageCalculator : IDisposable
    {
        (double MachineCpuUsage, double ProcessCpuUsage, double? MachineIoWait) Calculate();

        void Init();
    }
}
