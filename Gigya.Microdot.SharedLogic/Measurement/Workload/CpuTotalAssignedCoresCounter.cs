using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    /// <summary>
    /// The perf counter of Total CPU usage of cores assigned to the process by affinity.
    /// The result is: SUM(CPU-total-i) / Count, where Count is number of assigned CPUs.
    /// </summary>
    public class CpuTotalAssignedCoresCounter : IDisposable
    {
        private readonly List<PerformanceCounter> _counters;


        /// <summary>
        /// Initialize counter for current process
        /// </summary>
        public CpuTotalAssignedCoresCounter()
            : this(Process.GetCurrentProcess())
        {
        }

        /// <summary>
        /// Initialize counter considering process affinity.
        /// Limited to 64 bit affinity mask.
        /// </summary>
        public CpuTotalAssignedCoresCounter(Process p)
        {
            _counters = new List<PerformanceCounter>(2 /* reasonable for a service with an affinity */);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var index in p.ProcessorAffinityList())
                    _counters.Add(new PerformanceCounter("Processor", "% Processor Time", $"{index}"));
            }
        }

        /// <summary>
        /// Get the current value of counter, or Null if exceptional.
        /// </summary>
        public double? GetValue()
        {
            try
            {
                
                return Math.Round(_counters.Sum(c => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? c.NextValue() : double.NaN) / _counters.Count, 2);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Dispose the obtained counters.
        /// </summary>
        public void Dispose()
        {
            foreach (var counter in _counters)
                counter.Dispose();
        }
    }
}
