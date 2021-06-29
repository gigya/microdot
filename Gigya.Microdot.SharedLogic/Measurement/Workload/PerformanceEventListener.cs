using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public class PerformanceEventListener : EventListener
    {
        private const string Runtime = "System.Runtime";
        private const string EventCounters = "EventCounters";

        private readonly Func<WorkloadMetricsConfig> _getConfig;
        private readonly IDateTime _dateTime;
        private readonly ILog _log;
        private readonly Dictionary<string, double> _counters;
        public PerformanceEventListener(Func<WorkloadMetricsConfig> getConfig, IDateTime dateTime, ILog log)
        {
            _log = log;
            _getConfig = getConfig;
            _dateTime = dateTime;
            _counters = new Dictionary<string, double>();
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

            _counters.Add(eventCounter, 0);

            return true;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals(Runtime))
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
                        var counter = GetRelevantMetric(eventPayload);
                        if (_counters.ContainsKey(counter.Name))
                            _counters[counter.Name] = counter.Value;
                    }
                }
            }
        }

        private (string Name, double Value) GetRelevantMetric(IDictionary<string, object> eventPayload)
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
            string counterName = NormalizeCounterName(performanceCounterName);
            var translationDictionary = _getConfig().PerformanceCountersToEventCounters;
            if (!translationDictionary.ContainsKey(counterName))
                return null;

            string eventCounter = translationDictionary[counterName];
            if (_counters.ContainsKey(eventCounter))
                return _counters[eventCounter];

            return null;
        }
    }
}