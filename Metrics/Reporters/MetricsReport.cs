using System;
using System.Threading;
using Metrics.MetricData;

namespace Metrics.Reporters
{
    public interface MetricsReport : Utils.IHideObjectMembers
    {
        void RunReport(MetricsData metricsData, Func<HealthStatus> healthStatus, CancellationToken token);
    }
}
