using System;
using System.Diagnostics;


namespace Metrics.EventCounters.Linux.CPU
{
    public static class CpuHelper
    {
        public static ICpuUsageCalculator GetOSCpuUsageCalculator()
        {
            ICpuUsageCalculator calculator;

            calculator = new LinuxCpuUsageCalculator();

            calculator.Init();
            return calculator;
        }
        public static long GetNumberOfActiveCores(Process process)
        {
            try
            {
                return NumberOfSetBits(process.ProcessorAffinity.ToInt64());
            }
            catch (NotSupportedException)
            {
                return ProcessorInfo.ProcessorCount;
            }
            catch (Exception e)
            {
                return ProcessorInfo.ProcessorCount;
            }
        }
        private static long NumberOfSetBits(long i)
        {
            i = i - ((i >> 1) & 0x5555555555555555);
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
            catch (Exception e)
            {

                return (0, 0);
            }
        }
    }
}
