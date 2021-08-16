using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public class PerformanceEventListener : EventListener
    {
        private static readonly string[] EventSources = {
            "System.Runtime",
            "System.Net.NameResolution",
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Net.Security",
            "Gigya.EventCounters"
        };

        private const string EventCounters = "EventCounters";

        private readonly Func<WorkloadMetricsConfig> _getConfig;
        private readonly Dictionary<string, Counter> _counters;
        private readonly Dictionary<string, Counter> _perfCounters;

        public PerformanceEventListener(Func<WorkloadMetricsConfig> getConfig)
        {
            _getConfig = getConfig;
            _counters = new Dictionary<string, Counter>();
            _perfCounters = new Dictionary<string, Counter>();
        }

        public bool Subscribe(string performanceCounterName)
        {
            string counterName = NormalizeCounterName(performanceCounterName);

            var translationDictionary = _getConfig().PerformanceCountersToEventCounters;
            if (translationDictionary == null || !translationDictionary.ContainsKey(counterName))
                return false;
            string eventCounter = translationDictionary[counterName];
            if (_counters.ContainsKey(eventCounter))
                return true;

            Counter counter = new Counter(eventCounter, performanceCounterName);
            _counters.Add(eventCounter, counter);
            _perfCounters.Add(performanceCounterName, counter);

            return true;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (EventSources.Contains(eventSource.Name))
            {
                Dictionary<string, string> refreshInterval = new Dictionary<string, string> { { "EventCounterIntervalSec", "1" } };
                EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All, refreshInterval);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName is EventCounters)
            {
                for (int i = 0; i < eventData.Payload?.Count; i++)
                {
                    if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
                    {
                        var counter = GetMetric(eventPayload);
                        if (_counters.ContainsKey(counter.Name))
                            _counters[counter.Name].Value = counter.Value;
                    }
                }
            }
        }

        private (string Name, double Value) GetMetric(IDictionary<string, object> eventPayload)
        {
            string name = string.Empty;
            double value = 0;
            foreach (KeyValuePair<string, object> payload in eventPayload)
            {
                switch (payload.Key)
                {
                    case "Name":
                        name = payload.Value.ToString();
                        break;
                    case "Mean":
                    case "Increment":
                        value = Convert.ToDouble(payload.Value);
                        break;
                }
            }
            return (name, value);
        }

        private string NormalizeCounterName(string name)
        {
            return name.Replace("#", "").Replace("%", "").Trim().Replace(" ", "_").Replace("/", "_").ToLower();
        }

        public double? ReadPerfCounter(string performanceCounterName)
        {
            if (_perfCounters.ContainsKey(performanceCounterName))
                return _perfCounters[performanceCounterName].Value;

            return null;
        }
    }
}