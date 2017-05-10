using System;
using System.Collections.Generic;

using Metrics;

namespace Gigya.Microdot.SharedLogic.Monitor
{
    public interface IHealthMonitor
    {
        void Dispose();
        ComponentHealthMonitor Get(string component);
        Dictionary<string, string> GetData(string component);
        ComponentHealthMonitor SetHealthFunction(string component, Func<HealthCheckResult> check, Func<Dictionary<string, string>> healthData = null);
    }
}