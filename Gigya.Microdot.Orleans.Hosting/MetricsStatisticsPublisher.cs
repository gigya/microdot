using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Metrics;

using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;

namespace Gigya.Microdot.Orleans.Hosting
{
    internal class MetricsStatisticsPublisher : IConfigurableStatisticsPublisher , IConfigurableSiloMetricsDataPublisher, IConfigurableClientMetricsDataPublisher , IProvider
    {
        private readonly ConcurrentDictionary<string, double> latestMetricValues = new ConcurrentDictionary<string, double>();

        /// <summary>Contains a set of gauges whose lambda getter methods fetch their values from <see cref="latestMetricValues"/></summary>
        private readonly MetricsContext context = Metric.Context("Silo");
        
        public async Task ReportStats(List<ICounter> statsCounters)
        {
            if (statsCounters == null) // todo: check if Orleans ever sends null; otherwise remove
                return;

            /*
            var dict=statsCounters.GroupBy(a => a.Storage).ToDictionary(a => a.Key, a => a.ToArray());
            var stats = dict[CounterStorage.LogAndTable].Select(a => a.Name + " : " + (a.IsValueDelta ? a.GetDeltaString() : a.GetValueString())).ToArray();
            var allCounters = string.Join("\n", stats.OrderBy(a=>a));
            */
            await Task.Factory.StartNew(() => PublishMetrics(statsCounters));
        }

        private void PublishMetrics(IEnumerable<ICounter> statsCounters)
        {
            // Note: some ICounter's hold non-numeric values and cannot be processed. We can easily identify MOST of
            // them by filtering on counters that can be written to a table.
            foreach (var metric in statsCounters.Where(a => a.Storage == CounterStorage.LogAndTable))
            {
                var value = metric.IsValueDelta ? metric.GetDeltaString() : metric.GetValueString();

                double newVal;

                if (double.TryParse(value, out newVal))
                {
                    double oldValue;
                    if (latestMetricValues.TryGetValue(metric.Name, out oldValue))
                    {
                        if (metric.IsValueDelta)
                        {
                            latestMetricValues.AddOrUpdate(metric.Name, key => newVal, (k, v) => v + newVal);
                        }
                        else
                        {
                            latestMetricValues[metric.Name] = newVal;
                        }
                    }
                    else // new counter discovered
                    {
                        latestMetricValues[metric.Name] = newVal;
                        context.Gauge(metric.Name, () => latestMetricValues[metric.Name], Unit.None);
                    }
                }
            }
        }

        public Task Init(bool isSilo, string storageConnectionString, string deploymentId, string address, string siloName, string hostName)
        {
            return TaskDone.Done;
        }

        void IConfigurableStatisticsPublisher.AddConfiguration(string deploymentId, bool isSilo, string siloName, SiloAddress address, IPEndPoint gateway, string hostName)
        {
            
        }

        public Task Init(string deploymentId, string storageConnectionString, SiloAddress siloAddress, string siloName, IPEndPoint gateway, string hostName)
        {
            return TaskDone.Done;
        }

        public Task ReportMetrics(ISiloPerformanceMetrics metricsData)
        {
            return TaskDone.Done;
        }

        void IConfigurableSiloMetricsDataPublisher.AddConfiguration(string deploymentId, bool isSilo, string siloName, SiloAddress address, IPEndPoint gateway, string hostName)
        {
          
        }

        public Task Init(ClientConfiguration config, IPAddress address, string clientId)
        {
            return TaskDone.Done; 
        }

        public Task ReportMetrics(IClientPerformanceMetrics metricsData)
        {
            return TaskDone.Done; 
        }

        public void AddConfiguration(string deploymentId, string hostName, string clientId, IPAddress address)
        {
          
        }

        public Task Init(string name, IProviderRuntime providerRuntime, IProviderConfiguration config)
        {
            Name = name;
            return TaskDone.Done;
        }

        public Task Close()
        {
            Metric.ShutdownContext("Silo");
            return TaskDone.Done;
        }

        public string Name { get; private set; }
    }
}