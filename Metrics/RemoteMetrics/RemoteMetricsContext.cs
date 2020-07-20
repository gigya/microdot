using System;
using System.Threading;
using System.Threading.Tasks;
using Metrics.Core;
using Metrics.Json;
using Metrics.MetricData;
using Metrics.Utils;

namespace Metrics.RemoteMetrics
{
    public sealed class RemoteMetricsContext : ReadOnlyMetricsContext, MetricsDataProvider
    {
        private readonly Scheduler scheduler;

        private MetricsData currentData = MetricsData.Empty;

        public RemoteMetricsContext(Uri remoteUri, TimeSpan updateInterval, Func<string, JsonMetricsContext> deserializer)
            : this(new ActionScheduler(), remoteUri, updateInterval, deserializer)
        { }

        public RemoteMetricsContext(Scheduler scheduler, Uri remoteUri, TimeSpan updateInterval, Func<string, JsonMetricsContext> deserializer)
        {
            this.scheduler = scheduler;
            this.scheduler.Start(updateInterval, c => UpdateMetrics(remoteUri, deserializer, c));
        }

        private async Task UpdateMetrics(Uri remoteUri, Func<string, JsonMetricsContext> deserializer, CancellationToken token)
        {
            try
            {
                var remoteContext = await HttpRemoteMetrics.FetchRemoteMetrics(remoteUri, deserializer, token).ConfigureAwait(false);
                remoteContext.Environment.Add("RemoteUri", remoteUri.ToString());
                remoteContext.Environment.Add("RemoteVersion", remoteContext.Version);
                remoteContext.Environment.Add("RemoteTimestamp", Clock.FormatTimestamp(remoteContext.Timestamp));

                this.currentData = remoteContext.ToMetricsData();
            }
            catch (Exception x)
            {
                MetricsErrorHandler.Handle(x, "Error updating metrics data from " + remoteUri);
                this.currentData = MetricsData.Empty;
            }
        }

        public override MetricsDataProvider DataProvider { get { return this; } }
        public MetricsData CurrentMetricsData { get { return this.currentData; } }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (this.scheduler) { }
            }
            base.Dispose(disposing);
        }
    }
}
