using System;

namespace Metrics.EventCounters.Linux.CPU
{
    public abstract class CpuUsageCalculator<T> : ICpuUsageCalculator where T : ProcessInfo
    {
        public readonly (double MachineCpuUsage, double ProcessCpuUsage, double? MachineIoWait) _emptyCpuUsage = (0, 0, null);
        public readonly object _locker = new object();

        public (double MachineCpuUsage, double ProcessCpuUsage, double? MachineIoWait)? LastCpuUsage;

        public T PreviousInfo;

        public void Init()
        {
            PreviousInfo = GetProcessInfo();
        }

        public abstract (double MachineCpuUsage, double? MachineIoWait) CalculateMachineCpuUsage(T processInfo);

        public (double MachineCpuUsage, double ProcessCpuUsage, double? MachineIoWait) Calculate()
        {
            // this is a pretty quick method (sys call only), and shouldn't be
            // called heavily, so it is easier to make sure that this is thread
            // safe by just holding a lock.
            lock (_locker)
            {
                if (PreviousInfo == null)
                    return _emptyCpuUsage;

                var currentInfo = GetProcessInfo();
                if (currentInfo == null)
                    return _emptyCpuUsage;

                var machineCpuUsage = CalculateMachineCpuUsage(currentInfo);
                var processCpuUsage = CalculateProcessCpuUsage(currentInfo, machineCpuUsage.MachineCpuUsage);

                PreviousInfo = currentInfo;

                LastCpuUsage = (machineCpuUsage.MachineCpuUsage, processCpuUsage, machineCpuUsage.MachineIoWait);
                return (machineCpuUsage.MachineCpuUsage, processCpuUsage, machineCpuUsage.MachineIoWait);
            }
        }

        public abstract T GetProcessInfo();

        public double CalculateProcessCpuUsage(ProcessInfo currentInfo, double machineCpuUsage)
        {
            var processorTimeDiff = currentInfo.TotalProcessorTimeTicks - PreviousInfo.TotalProcessorTimeTicks;
            var timeDiff = currentInfo.TimeTicks - PreviousInfo.TimeTicks;
            if (timeDiff <= 0)
            {
                return LastCpuUsage?.ProcessCpuUsage ?? 0;
            }

            if (currentInfo.ActiveCores <= 0)
            {
                return LastCpuUsage?.ProcessCpuUsage ?? 0;
            }

            var processCpuUsage = (processorTimeDiff * 100.0) / timeDiff / currentInfo.ActiveCores;
            if ((int)currentInfo.ActiveCores == ProcessorInfo.ProcessorCount)
            {
                // min as sometimes +-1% due to time sampling
                processCpuUsage = Math.Min(processCpuUsage, machineCpuUsage);
            }

            return Math.Min(100, processCpuUsage);
        }

        public void Dispose()
        {
        }
    }
}
