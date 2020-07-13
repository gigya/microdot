using Metrics.MetricData;
using System;
using System.Text;
using System.Threading;

namespace Metrics.Reporters
{
    public class StringReport : HumanReadableReport
    {
        public static string RenderMetrics(MetricsData metricsData, Func<HealthStatus> healthStatus)
        {
            var report = new StringReport();
            report.RunReport(metricsData, healthStatus, CancellationToken.None);
            return report.Result;
        }

        private StringBuilder buffer;

        protected override void StartReport(string contextName)
        {
            this.buffer = new StringBuilder();
            base.StartReport(contextName);
        }
        protected override void WriteLine(string line, params string[] args)
        {
            this.buffer.AppendLine(string.Format(line, args));
        }

        public string Result => this.buffer.ToString();
    }
}
