using System;
using System.Net.Http;
using System.Threading.Tasks;
using Metrics.Core;
using Metrics.Json;
using Metrics.MetricData;
using Metrics.Utils;

namespace Metrics.RemoteMetrics
{
    public sealed class RemoteMetricsContext : ReadOnlyMetricsContext, MetricsDataProvider
    {
        private readonly Scheduler _scheduler;
        private readonly HttpClient _httpClient;
        private MetricsData _currentData = MetricsData.Empty;

        public RemoteMetricsContext(Uri remoteUri, TimeSpan updateInterval, Func<string, JsonMetricsContext> deserializer)
            : this(new ActionScheduler(), remoteUri, updateInterval, deserializer)
        { }

        public RemoteMetricsContext(Scheduler scheduler, Uri remoteUri, TimeSpan updateInterval, Func<string, JsonMetricsContext> deserializer)
        {
            _scheduler = scheduler;
            _scheduler.Start(updateInterval, c => UpdateMetrics(remoteUri, deserializer));
        }

        private async Task UpdateMetrics(Uri remoteUri, Func<string, JsonMetricsContext> deserializer)
        {
            try
            {
                string response = await _httpClient.GetStringAsync(remoteUri);
                JsonMetricsContext remoteContext = deserializer(response);
                remoteContext.Environment.Add("RemoteUri", remoteUri.ToString());
                remoteContext.Environment.Add("RemoteVersion", remoteContext.Version);
                remoteContext.Environment.Add("RemoteTimestamp", Clock.FormatTimestamp(remoteContext.Timestamp));
            
                _currentData = remoteContext.ToMetricsData();
            }
            catch (Exception x)
            {
                MetricsErrorHandler.Handle(x, "Error updating metrics data from " + remoteUri);
                _currentData = MetricsData.Empty;
            }
        }

        public override MetricsDataProvider DataProvider => this;
        public MetricsData CurrentMetricsData => _currentData;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();

                using (_scheduler) { }
            }
            base.Dispose(disposing);
        }
    }
}
