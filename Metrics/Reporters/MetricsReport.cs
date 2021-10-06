using Metrics.MetricData;
using System;
using System.Threading;

namespace Metrics.Reporters
{
    public interface MetricsReport : Utils.IHideObjectMembers
    {
        void RunReport(MetricsData metricsData, Func<HealthStatus> healthStatus, CancellationToken token);
    }
}
