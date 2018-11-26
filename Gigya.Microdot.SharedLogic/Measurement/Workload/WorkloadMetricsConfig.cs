using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    [ConfigurationRoot("WorkloadMetrics", RootStrategy.ReplaceClassNameWithPath)]
    public class WorkloadMetricsConfig : IConfigObject
    {
        /// <summary>
        /// Whether to read performance counters from windows. If false, all runtime metric values will be zero
        /// </summary>
        public bool ReadPerformanceCounters { get; set; } = false;

        /// <summary>
        /// Max CPU usage (in percent) to be considered healthy. If CPU usage is higher, service will declare itself as unhealthy after period of <See cref="MinUnhealthyDuration"/>
        /// </summary>
        public double MaxHealthyCpuUsage { get; set; } = 90;

        /// <summary>
        /// Max number of threads to be considered healthy. If threads count is higher, service will declare itself as unhealthy after period of <See cref="MinUnhealthyDuration"/>
        /// </summary>
        public int MaxHealthyThreadsCount { get; set; } = 100;

        /// <summary>
        /// Max Orleans request queue length to be considered healthy. If queue length is longer, service will declare itself as unhealthy after period of <See cref="MinUnhealthyDuration"/>
        /// </summary>
        public int MaxHealthyOrleansQueueLength { get; set; } = 100;

        /// <summary>
        /// Service will report itself as unhealthy only if some unhelthy metric is unhealthy for at least the specified duration
        /// </summary>
        public TimeSpan MinUnhealthyDuration { get; set; } = TimeSpan.FromMinutes(3);
    }
}
