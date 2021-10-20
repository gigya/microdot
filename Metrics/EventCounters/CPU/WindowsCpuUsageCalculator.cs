using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Metrics.EventCounters.CPU
{

    public class WindowsCpuUsageCalculator : CpuUsageCalculator<WindowsInfo>
    {
        public override (double MachineCpuUsage, double? MachineIoWait) CalculateMachineCpuUsage(WindowsInfo windowsInfo)
        {
            var systemIdleDiff = windowsInfo.SystemIdleTime - PreviousInfo.SystemIdleTime;
            var systemKernelDiff = windowsInfo.SystemKernelTime - PreviousInfo.SystemKernelTime;
            var systemUserDiff = windowsInfo.SystemUserTime - PreviousInfo.SystemUserTime;
            var sysTotal = systemKernelDiff + systemUserDiff;

            double machineCpuUsage = 0;
            if (sysTotal > 0)
            {
                machineCpuUsage = (sysTotal - systemIdleDiff) * 100.00 / sysTotal;
            }

            return (machineCpuUsage, null);
        }

        public override WindowsInfo GetProcessInfo()
        {
            var systemIdleTime = new FileTime();
            var systemKernelTime = new FileTime();
            var systemUserTime = new FileTime();
            if (GetSystemTimes(ref systemIdleTime, ref systemKernelTime, ref systemUserTime) == false)
            {               
                return null;
            }

            return new WindowsInfo
            {
                SystemIdleTime = GetTime(systemIdleTime),
                SystemKernelTime = GetTime(systemKernelTime),
                SystemUserTime = GetTime(systemUserTime)
            };
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetSystemTimes(
            ref FileTime lpIdleTime,
            ref FileTime lpKernelTime,
            ref FileTime lpUserTime);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong GetTime(FileTime fileTime)
        {
            return ((ulong)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FileTime
        {
            public int dwLowDateTime;
            public int dwHighDateTime;
        }
    }

}
