using System.Collections.Generic;

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
            var mask = (ulong)p.ProcessorAffinity.ToInt64();
            for (var i = 0; i < 64; i++)
                if ((mask & 1ul << i) > 0)
                    yield return i;
        }

    }
}
