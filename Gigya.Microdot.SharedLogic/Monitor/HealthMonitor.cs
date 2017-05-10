using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Metrics;

namespace Gigya.Microdot.SharedLogic.Monitor
{
    public sealed class HealthMonitor : IDisposable, IHealthMonitor
    {
        private static readonly ConcurrentDictionary<string, ComponentHealthMonitor> _componentMonitors = new ConcurrentDictionary<string, ComponentHealthMonitor>();

        public ComponentHealthMonitor SetHealthFunction(string component, Func<HealthCheckResult> check, Func<Dictionary<string, string>> healthData=null)
        {
            var componentHealthMonitor = _componentMonitors.GetOrAdd(component, _ => new ComponentHealthMonitor(component, check));
            componentHealthMonitor.Activate();
            componentHealthMonitor.SetHealthFunction(check);
            componentHealthMonitor.SetHealthData(healthData);
            return componentHealthMonitor;
        }

        public ComponentHealthMonitor Get(string component)
        {
            return _componentMonitors.GetOrAdd(component, _ => new ComponentHealthMonitor(component, HealthCheckResult.Healthy));
        }

        public void Dispose()
        {
             _componentMonitors.Clear();
             HealthChecks.UnregisterAllHealthChecks();
        }


        /// <summary>
        /// Return health data for specified component
        /// </summary>
        public Dictionary<string, string> GetData(string component)
        {
            if (_componentMonitors.ContainsKey(component))
                return _componentMonitors[component].GetHealthData();
            else
                return new Dictionary<string, string>();
        }

        public static string GetMessages(Exception ex)
        {
            var messages = new List<string>();
            var current = ex;
            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }
            return string.Join(" --> ", messages);
        }
    }

}
