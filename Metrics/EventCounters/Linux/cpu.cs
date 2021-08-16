using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;

namespace Metrics.EventCounters.Linux
{
    #region Machine Info
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

    public class WindowsInfo : ProcessInfo
    {
        public ulong SystemIdleTime { get; set; }

        public ulong SystemKernelTime { get; set; }

        public ulong SystemUserTime { get; set; }
    }

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

    public class MacInfo : ProcessInfo
    {
        public ulong TotalTicks { get; set; }

        public ulong IdleTicks { get; set; }
    }

    #endregion


    #region Bits
    public static class Bits
    {
        //https://stackoverflow.com/questions/2709430/count-number-of-bits-in-a-64-bit-long-big-integer
        public static long NumberOfSetBits(long i)
        {
            i = i - ((i >> 1) & 0x5555555555555555);
            i = (i & 0x3333333333333333) + ((i >> 2) & 0x3333333333333333);
            return (((i + (i >> 4)) & 0xF0F0F0F0F0F0F0F) * 0x101010101010101) >> 56;
        }


        // Code taken from http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogDeBruijn

        private static readonly byte[] MultiplyDeBruijnBitPosition =
                {
                    0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
                    8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
                };

        private static readonly byte[] DeBruijnBytePos64 =
            {
                0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7, 0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7,
                7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6, 7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7
            };

        private static readonly byte[] DeBruijnBytePos32 =
            {
                0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1, 3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1
            };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MostSignificantBit(uint n)
        {
            n |= n >> 1; // first round down to one less than a power of 2 
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;

            return MultiplyDeBruijnBitPosition[(n * 0x07C4ACDDU) >> 27];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MostSignificantBit(int n)
        {
            n |= n >> 1; // first round down to one less than a power of 2 
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;

            return MultiplyDeBruijnBitPosition[(uint)(n * 0x07C4ACDDU) >> 27];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MostSignificantBit(long nn)
        {
            unchecked
            {
                if (nn == 0) return 0;

                ulong n = (ulong)nn;
                int msb = 0;

                if ((n & 0xFFFFFFFF00000000L) != 0)
                {
                    n >>= (1 << 5);
                    msb += (1 << 5);
                }

                if ((n & 0xFFFF0000) != 0)
                {
                    n >>= (1 << 4);
                    msb += (1 << 4);
                }

                // Now we find the most significant bit in a 16-bit word.

                n |= n << 16;
                n |= n << 32;

                ulong y = n & 0xFF00F0F0CCCCAAAAL;

                ulong t = 0x8000800080008000L & (y | ((y | 0x8000800080008000L) - (n ^ y)));

                t |= t << 15;
                t |= t << 30;
                t |= t << 60;

                return (int)((ulong)msb + (t >> 60));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MostSignificantBit(ulong n)
        {
            if (n == 0) return 0;

            ulong msb = 0;

            if ((n & 0xFFFFFFFF00000000L) != 0)
            {
                n >>= (1 << 5);
                msb += (1 << 5);
            }

            if ((n & 0xFFFF0000) != 0)
            {
                n >>= (1 << 4);
                msb += (1 << 4);
            }

            // Now we find the most significant bit in a 16-bit word.

            n |= n << 16;
            n |= n << 32;

            ulong y = n & 0xFF00F0F0CCCCAAAAL;

            ulong t = 0x8000800080008000L & (y | ((y | 0x8000800080008000L) - (n ^ y)));

            t |= t << 15;
            t |= t << 30;
            t |= t << 60;

            return (int)(msb + (t >> 60));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroes(int n)
        {
            if (n == 0)
                return 32;
            return 31 - MostSignificantBit(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroes(uint n)
        {
            if (n == 0)
                return 32;
            return 31 - MostSignificantBit(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroes(long n)
        {
            if (n == 0)
                return 64;
            return 63 - MostSignificantBit(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroes(ulong n)
        {
            if (n == 0)
                return 64;
            return 63 - MostSignificantBit(n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CeilLog2(int n)
        {
            int v = n;
            v |= v >> 1; // first round down to one less than a power of 2 
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            int pos = MultiplyDeBruijnBitPosition[(uint)(v * 0x07C4ACDDU) >> 27];
            if (n > (v & ~(v >> 1)))
                return pos + 1;

            return pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CeilLog2(uint n)
        {
            uint v = n;
            v |= v >> 1; // first round down to one less than a power of 2 
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            int pos = MultiplyDeBruijnBitPosition[(uint)(v * 0x07C4ACDDU) >> 27];
            if (n > (v & ~(v >> 1)))
                return pos + 1;

            return pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorLog2(uint n)
        {
            uint v = n;
            v |= v >> 1; // first round down to one less than a power of 2 
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            return MultiplyDeBruijnBitPosition[(uint)(v * 0x07C4ACDDU) >> 27];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorLog2(int n)
        {
            int v = n;
            v |= v >> 1; // first round down to one less than a power of 2 
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            return MultiplyDeBruijnBitPosition[(uint)(v * 0x07C4ACDDU) >> 27];
        }

        private static readonly int[] powerOf2Table =
        {
              0,   1,   2,   4,   4,   8,   8,   8,   8,  16,  16,  16,  16,  16,  16,  16,
             16,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,  32,
             32,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,
             64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,  64,
             64, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256,
            256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256, 256
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PowerOf2(int v)
        {
            if (v < powerOf2Table.Length)
                return powerOf2Table[v];
            return PowerOf2Internal(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long PowerOf2(long v)
        {
            if (v < powerOf2Table.Length)
                return powerOf2Table[v];
            return PowerOf2Internal(v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PowerOf2Internal(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;

            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long PowerOf2Internal(long v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            v++;

            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft32(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateRight32(uint value, int count)
        {
            return (value >> count) | (value << (32 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateLeft64(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong RotateRight64(ulong value, int count)
        {
            return (value >> count) | (value << (64 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SwapBytes(uint value)
        {
            return ((value & 0xff000000) >> 24) |
                   ((value & 0x00ff0000) >> 8) |
                   ((value & 0x0000ff00) << 8) |
                   ((value & 0x000000ff) << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SwapBytes(int value)
        {
            return (int)SwapBytes((uint)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SwapBytes(long value)
        {
            return (long)SwapBytes((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SwapBytes(ulong value)
        {
            return (((value & 0xff00000000000000UL) >> 56) |
                    ((value & 0x00ff000000000000UL) >> 40) |
                    ((value & 0x0000ff0000000000UL) >> 24) |
                    ((value & 0x000000ff00000000UL) >> 8) |
                    ((value & 0x00000000ff000000UL) << 8) |
                    ((value & 0x0000000000ff0000UL) << 24) |
                    ((value & 0x000000000000ff00UL) << 40) |
                    ((value & 0x00000000000000ffUL) << 56));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroesInBytes(ulong value)
        {
            return DeBruijnBytePos64[((value & (ulong)(-(long)value)) * 0x0218A392CDABBD3FUL) >> 58];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroesInBytes(long value)
        {
            return DeBruijnBytePos64[((ulong)(value & -value) * 0x0218A392CDABBD3FUL) >> 58];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroesInBytes(uint value)
        {
            return DeBruijnBytePos32[((value & (uint)(-(int)value)) * 0x077CB531U) >> 27];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroesInBytes(int value)
        {
            return DeBruijnBytePos32[((uint)(value & -value) * 0x077CB531U) >> 27];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(int value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }
    }
    #endregion


    public class SystemTime
    {
        private static readonly SystemTime Instance = new SystemTime();

        /// <summary>
        /// Tests now run in parallel so this is no longer static to mitigate the possibility of getting incorrect results. Use DocumentDatabase.Time instead.
        /// </summary>
        public Func<DateTime> UtcDateTime;

        public Action<int> WaitCalled;

        public DateTime GetUtcNow()
        {
            var temp = UtcDateTime;
            return temp?.Invoke() ?? DateTime.UtcNow;
        }

        public static DateTime UtcNow => Instance.GetUtcNow();
    }

    #region PlatformDetails
    public static class PlatformDetails
    {
        public static readonly bool IsWindows8OrNewer;

        private static readonly bool IsWindows10OrNewer;

        public static readonly bool Is32Bits = IntPtr.Size == sizeof(int);

        public static readonly bool RunningOnPosix = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                                                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static readonly bool RunningOnMacOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static readonly bool RunningOnLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static readonly bool CanPrefetch;
        public static readonly bool CanDiscardMemory;
        internal static readonly bool CanUseHttp2;

        public static bool RunningOnDocker;

        static PlatformDetails()
        {
            RunningOnDocker = string.Equals(Environment.GetEnvironmentVariable("RAVEN_IN_DOCKER"), "true", StringComparison.OrdinalIgnoreCase);

            if (TryGetWindowsVersion(out var version))
            {
                IsWindows8OrNewer = version >= 6.19M;
                IsWindows10OrNewer = version >= 10M;
            }

            CanPrefetch = IsWindows8OrNewer || RunningOnPosix;
            CanDiscardMemory = IsWindows10OrNewer || RunningOnPosix;
            CanUseHttp2 = IsWindows10OrNewer || RunningOnPosix;
        }

        private static bool TryGetWindowsVersion(out decimal version)
        {
            version = -1M;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
                return false;

            try
            {
                const string winString = "Windows ";
                var os = RuntimeInformation.OSDescription;

                var idx = os.IndexOf(winString, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return false;

                var ver = os.Substring(idx + winString.Length);

                // remove second occurence of '.' (win 10 might be 10.123.456)
                var index = ver.IndexOf('.', ver.IndexOf('.') + 1);
                ver = string.Concat(ver.Substring(0, index), ver.Substring(index + 1));

                return decimal.TryParse(ver, NumberStyles.Any, CultureInfo.InvariantCulture, out version);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }

    #endregion



    #region processor info 
    public class ProcessorInfo
    {
        public static readonly int ProcessorCount = GetProcessorCount();

        public static int GetProcessorCount()
        {
            return Environment.ProcessorCount;
        }
    }

    #endregion 
    #region CPU Helper
    public static class CpuHelper
    {
       // private static readonly Logger Logger = LoggingSource.Instance.GetLogger<MachineResources>("Server");

        internal static ICpuUsageCalculator GetOSCpuUsageCalculator()
        {
            ICpuUsageCalculator calculator;
            //if (PlatformDetails.RunningOnPosix == false)
            //{
            //    calculator = new WindowsCpuUsageCalculator();
            //}
            //else if (PlatformDetails.RunningOnMacOsx)
            //{
            //   // calculator = new MacInfoCpuUsageCalculator();
            //}
            //else
            //{
                calculator = new LinuxCpuUsageCalculator();
           // }
            calculator.Init();
            return calculator;
        }

        //internal static ExtensionPointCpuUsageCalculator GetExtensionPointCpuUsageCalculator(
        //    JsonContextPool contextPool,
        //    MonitoringConfiguration configuration,
        //    NotificationCenter.NotificationCenter notificationCenter)
        //{
        //    var extensionPoint = new ExtensionPointCpuUsageCalculator(
        //        contextPool,
        //        configuration.CpuUsageMonitorExec,
        //        configuration.CpuUsageMonitorExecArguments,
        //        notificationCenter);



        //    return extensionPoint;
        //}

        public static long GetNumberOfActiveCores(Process process)
        {
            try
            {
                return Bits.NumberOfSetBits(process.ProcessorAffinity.ToInt64());
            }
            catch (NotSupportedException)
            {
                return ProcessorInfo.ProcessorCount;
            }
            catch (Exception e)
            {
                //if (Logger.IsInfoEnabled)
                //    Logger.Info("Failure to get the number of active cores", e);

                return ProcessorInfo.ProcessorCount;
            }
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
                //if (Logger.IsInfoEnabled)
                //    Logger.Info($"Failure to get process times, error: {e.Message}", e);

                return (0, 0);
            }
        }
    }

    #endregion



    public interface ICpuUsageCalculator : IDisposable
    {
        (float MachineCpuUsage, float ProcessCpuUsage, float? MachineIoWait) Calculate();

        void Init();
    }

    public abstract class CpuUsageCalculator<T> : ICpuUsageCalculator where T : ProcessInfo
    {
        public readonly (float MachineCpuUsage, float ProcessCpuUsage, float? MachineIoWait) _emptyCpuUsage = (0, 0, (float?)null);
        // protected readonly Logger Logger = LoggingSource.Instance.GetLogger<MachineResources>("Server");
        public readonly object _locker = new object();

        public (float MachineCpuUsage, float ProcessCpuUsage, float? MachineIoWait)? LastCpuUsage;

        public T PreviousInfo;

        public void Init()
        {
            PreviousInfo = GetProcessInfo();
        }

        public abstract (float MachineCpuUsage, float? MachineIoWait) CalculateMachineCpuUsage(T processInfo);

        public (float MachineCpuUsage, float ProcessCpuUsage, float? MachineIoWait) Calculate()
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

        public float CalculateProcessCpuUsage(ProcessInfo currentInfo, float machineCpuUsage)
        {
            var processorTimeDiff = currentInfo.TotalProcessorTimeTicks - PreviousInfo.TotalProcessorTimeTicks;
            var timeDiff = currentInfo.TimeTicks - PreviousInfo.TimeTicks;
            if (timeDiff <= 0)
            {
                //overflow
                return LastCpuUsage?.ProcessCpuUsage ?? 0;
            }

            if (currentInfo.ActiveCores <= 0)
            {
                // shouldn't happen
                //if (Logger.IsInfoEnabled)
                //{
                //    Logger.Info($"ProcessCpuUsage == {currentInfo.ActiveCores}, OS: {RuntimeInformation.OSDescription}");
                //}

                return LastCpuUsage?.ProcessCpuUsage ?? 0;
            }

            float processCpuUsage = (float)(processorTimeDiff * 100.0) / timeDiff / currentInfo.ActiveCores;
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

    //internal class WindowsCpuUsageCalculator : CpuUsageCalculator<WindowsInfo>
    //{
    //    protected override (double MachineCpuUsage, float? MachineIoWait) CalculateMachineCpuUsage(WindowsInfo windowsInfo)
    //    {
    //        var systemIdleDiff = windowsInfo.SystemIdleTime - PreviousInfo.SystemIdleTime;
    //        var systemKernelDiff = windowsInfo.SystemKernelTime - PreviousInfo.SystemKernelTime;
    //        var systemUserDiff = windowsInfo.SystemUserTime - PreviousInfo.SystemUserTime;
    //        var sysTotal = systemKernelDiff + systemUserDiff;

    //        double machineCpuUsage = 0;
    //        if (sysTotal > 0)
    //        {
    //            machineCpuUsage = (sysTotal - systemIdleDiff) * 100.00 / sysTotal;
    //        }

    //        return (machineCpuUsage, null);
    //    }

    //    protected override WindowsInfo GetProcessInfo()
    //    {
    //        var systemIdleTime = new FileTime();
    //        var systemKernelTime = new FileTime();
    //        var systemUserTime = new FileTime();
    //        if (GetSystemTimes(ref systemIdleTime, ref systemKernelTime, ref systemUserTime) == false)
    //        {
    //            //if (Logger.IsInfoEnabled)
    //            //    Logger.Info("Failure when trying to get GetSystemTimes from Windows, error code was: " + Marshal.GetLastWin32Error());
    //            return null;
    //        }

    //        return new WindowsInfo
    //        {
    //            SystemIdleTime = GetTime(systemIdleTime),
    //            SystemKernelTime = GetTime(systemKernelTime),
    //            SystemUserTime = GetTime(systemUserTime)
    //        };
    //    }

    //    [return: MarshalAs(UnmanagedType.Bool)]
    //    [DllImport("kernel32.dll", SetLastError = true)]
    //    internal static extern bool GetSystemTimes(
    //        ref FileTime lpIdleTime,
    //        ref FileTime lpKernelTime,
    //        ref FileTime lpUserTime);

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    internal static ulong GetTime(FileTime fileTime)
    //    {
    //        return ((ulong)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    internal struct FileTime
    //    {
    //        public int dwLowDateTime;
    //        public int dwHighDateTime;
    //    }
    //}

    public class LinuxCpuUsageCalculator : CpuUsageCalculator<LinuxInfo>
    {
        private static char[] _separators = { ' ', '\t' };

        public override (float MachineCpuUsage, float? MachineIoWait) CalculateMachineCpuUsage(LinuxInfo linuxInfo)
        {
            float machineCpuUsage = 0;
            float? machineIoWait = 0;
            if (linuxInfo.TotalIdle >= PreviousInfo.TotalIdle &&
                linuxInfo.TotalWorkTime >= PreviousInfo.TotalWorkTime)
            {
                var idleDiff = linuxInfo.TotalIdle - PreviousInfo.TotalIdle;
                var workDiff = linuxInfo.TotalWorkTime - PreviousInfo.TotalWorkTime;
                var totalSystemWork = idleDiff + workDiff;
                var ioWaitDiff = linuxInfo.TotalIoWait - PreviousInfo.TotalIoWait;

                if (totalSystemWork > 0)
                {
                    machineCpuUsage = (float)(workDiff * 100.0) / totalSystemWork;
                    machineIoWait = (float?)(ioWaitDiff * 100.0) / totalSystemWork;
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

    //internal class MacInfoCpuUsageCalculator : CpuUsageCalculator<MacInfo>
    //{
    //    private static readonly unsafe int HostCpuLoadInfoSize = sizeof(host_cpu_load_info) / sizeof(uint);

    //    protected override (double MachineCpuUsage, double? MachineIoWait) CalculateMachineCpuUsage(MacInfo macInfo)
    //    {
    //        var totalTicksSinceLastTime = macInfo.TotalTicks - PreviousInfo.TotalTicks;
    //        var idleTicksSinceLastTime = macInfo.IdleTicks - PreviousInfo.IdleTicks;
    //        double machineCpuUsage = 0;
    //        if (totalTicksSinceLastTime > 0)
    //        {
    //            machineCpuUsage = (1.0d - (double)idleTicksSinceLastTime / totalTicksSinceLastTime) * 100;
    //        }

    //        return (machineCpuUsage, null);
    //    }

    //    protected override unsafe MacInfo GetProcessInfo()
    //    {
    //        var machPort = macSyscall.mach_host_self();
    //        var count = HostCpuLoadInfoSize;
    //        var hostCpuLoadInfo = new host_cpu_load_info();
    //        if (macSyscall.host_statistics64(machPort, (int)Flavor.HOST_CPU_LOAD_INFO, &hostCpuLoadInfo, &count) != 0)
    //        {
    //            if (Logger.IsInfoEnabled)
    //                Logger.Info("Failure when trying to get hostCpuLoadInfo from MacOS, error code was: " + Marshal.GetLastWin32Error());
    //            return null;
    //        }

    //        ulong totalTicks = 0;
    //        for (var i = 0; i < (int)CpuState.CPU_STATE_MAX; i++)
    //            totalTicks += hostCpuLoadInfo.cpu_ticks[i];

    //        return new MacInfo
    //        {
    //            TotalTicks = totalTicks,
    //            IdleTicks = hostCpuLoadInfo.cpu_ticks[(int)CpuState.CPU_STATE_IDLE]
    //        };
    //    }
    //}

    //internal class ExtensionPointCpuUsageCalculator : ICpuUsageCalculator
    //{
    //    private readonly CpuUsageExtensionPoint _inspector;

    //    public ExtensionPointCpuUsageCalculator(
    //        JsonContextPool contextPool,
    //        string exec,
    //        string args,
    //        NotificationCenter.NotificationCenter notificationCenter)
    //    {
    //        _inspector = new CpuUsageExtensionPoint(
    //            contextPool,
    //            exec,
    //            args,
    //            notificationCenter
    //        );
    //    }

    //    public (double MachineCpuUsage, double ProcessCpuUsage, double? MachineIoWait) Calculate()
    //    {
    //        var data = _inspector.Data;
    //        return (data.MachineCpuUsage, data.ProcessCpuUsage, null);
    //    }

    //    public void Init()
    //    {
    //        _inspector.Start();
    //    }

    //    public void Dispose()
    //    {
    //        _inspector.Dispose();
    //    }
    //}
}
