using System;
using System.Diagnostics;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    /// <summary>
    /// The process CPU usage perf counter per count of affinity assigned virtual cores.
    /// The value is: CPU_USAGE / number of virtual cores (from actual process affinity).
    /// </summary>
    public class CpuUsageCounterByProcess : PerformanceCounterByProcess
    {
        /// <summary>
        /// The counter of CPUs (virtual cores) assigned to the process with affinity
        /// </summary>
        public int AssignedCoresCount { get; }

        public CpuUsageCounterByProcess()
            : base(categoryName: "Process", counterName: "% Processor Time")
        {
            AssignedCoresCount = Process.GetCurrentProcess().ProcessorAffinityList().Count();
        }

        /// <summary>
        /// Get the current value of counter, or Null if exceptional conditions.
        /// </summary>
        public override double? GetValue()
        {
            double? value = base.GetValue(); // never throwing

            if (value != null)
                value = Math.Round(value.Value / AssignedCoresCount, 2);

            return value;
        }

    }
}
