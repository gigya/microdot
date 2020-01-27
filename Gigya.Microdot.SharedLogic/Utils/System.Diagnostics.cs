using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable CheckNamespace
namespace System.Diagnostics
{
    public static class ProcessExtensions
    {

        /// <summary>
        /// Enumerates the indexes of cores assgined to the current process by CPU affinity.
        /// </summary>
        public static IEnumerable<int> ProcessorAffinityList(this Process p)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                // Processor affinity for processes or threads is not supported on this platform. 
                // So just return all processors
            {
                for (int i = 0; i < Environment.ProcessorCount; i++)
                    yield return i;
                yield break;
            }

            var mask = (ulong)p.ProcessorAffinity.ToInt64();
            for (var i = 0; i < 64; i++)
                if ((mask & 1ul << i) > 0)
                    yield return i;
        }

    }
}
