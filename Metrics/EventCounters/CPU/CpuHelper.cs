using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Metrics.EventCounters.CPU
{
    public static class CpuHelper
    {
        public static ICpuUsageCalculator GetOSCpuUsageCalculator()
        {
            ICpuUsageCalculator calculator;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                calculator = new WindowsCpuUsageCalculator();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                calculator = new LinuxCpuUsageCalculator();
            else
                throw new NotSupportedException($"Platform '{RuntimeInformation.OSDescription}' not supported");
            
            calculator.Init();

            return calculator;
        }
        public static long GetNumberOfActiveCores(Process process)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return NumberOfSetBits(process.ProcessorAffinity.ToInt64());

                throw new NotSupportedException($"Platform '{RuntimeInformation.OSDescription}' not supported");
            }
            catch (NotSupportedException)
            {
                return ProcessorInfo.ProcessorCount;
            }
            catch
            {
                return ProcessorInfo.ProcessorCount;
            }
        }
        private static long NumberOfSetBits(long i)
        {
            i -= (i >> 1) & 0x5555555555555555;
            i = (i & 0x3333333333333333) + ((i >> 2) & 0x3333333333333333);
            return (((i + (i >> 4)) & 0xF0F0F0F0F0F0F0F) * 0x101010101010101) >> 56;
        }
        public static (long TotalProcessorTimeTicks, long TimeTicks) GetProcessTimes(Process process)
        {
            try
            {
                var timeTicks = SystemTime.UtcNow.Ticks;
                var totalProcessorTime = process.TotalProcessorTime.Ticks;
                return (TotalProcessorTimeTicks: totalProcessorTime, TimeTicks: timeTicks);
            }
            catch (NotSupportedException)
            {
                return (0, 0);
            }
            catch
            {

                return (0, 0);
            }
        }
    }
}
