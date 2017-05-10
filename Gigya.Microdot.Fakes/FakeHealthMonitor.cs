using System;
using System.Collections.Generic;

using Gigya.Microdot.SharedLogic.Monitor;

using Metrics;

namespace Gigya.Microdot.Fakes
{
    public class FakeHealthMonitor : IHealthMonitor
    {
        public Dictionary<string, Func<HealthCheckResult>> Monitors = new Dictionary<string, Func<HealthCheckResult>>();


        public void Dispose()
        {

        }


        public ComponentHealthMonitor Get(string component)
        {
            throw new NotImplementedException();
        }


        public Dictionary<string, string> GetData(string component)
        {
            throw new NotImplementedException();
        }


        public ComponentHealthMonitor SetHealthFunction(string component, Func<HealthCheckResult> check, Func<Dictionary<string, string>> healthData = null)
        {
            Monitors[component] = check;
            return new ComponentHealthMonitor(component, check);
        }
    }
}
