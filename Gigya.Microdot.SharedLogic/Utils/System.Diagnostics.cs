using Metrics.EventCounters.CPU;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if NET6_0_OR_GREATER
using System.Runtime.Versioning;
#endif
// ReSharper disable CheckNamespace
namespace System.Diagnostics
{
    public static class ProcessExtensions
    {

        /// <summary>
        /// Enumerates the indexes of cores assgined to the current process by CPU affinity.
        /// </summary>
#if NET6_0_OR_GREATER
        [SupportedOSPlatformGuard("windows")]
        [SupportedOSPlatformGuard("linux")]
        [SupportedOSPlatformGuard("iOS")]
#endif
        public static IEnumerable<int> ProcessorAffinityList(this Process p)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var mask = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ?
                    (ulong)p.ProcessorAffinity.ToInt64() :
                    (ulong)ProcessorInfo.ProcessorCount;

                for (var i = 0; i < 64; i++)
                    if ((mask & 1ul << i) > 0)
                        yield return i;
            }
            else
                throw new NotSupportedException($"Platform '{RuntimeInformation.OSDescription}' not supported");
        }

    }
}
