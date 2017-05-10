using System;
using System.Collections.Generic;

using Metrics;

namespace Gigya.Microdot.SharedLogic.Monitor
{
    public class ComponentHealthMonitor : IDisposable
    {
        private Func<HealthCheckResult> _healthFunction;
        private Func<Dictionary<string, string>> _getHealthData;
        private readonly string _component;
        private bool _active = false;

        public ComponentHealthMonitor(string component, Func<HealthCheckResult> func)
        {
            _component = component;
            SetHealthFunction(func);
        }

        public void Activate()
        {
            if (!_active)
            {
                HealthChecks.RegisterHealthCheck(_component, () => CheckFunction());
                _active = true;
            }
        }

        public void Deactivate()
        {
            if (_active)
            {
                HealthChecks.UnregisterHealthCheck(_component);
                _active = false;
            }
        }

        public void SetHealthFunction(Func<HealthCheckResult> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            _healthFunction = func;
        }

        private HealthCheckResult CheckFunction()
        {
            return _healthFunction.Invoke();
        }

        public void SetHealthData(Func<Dictionary<string, string>> getHealthData)
        {
            _getHealthData = getHealthData;
        }

        public Dictionary<string, string> GetHealthData()
        {
            if (_getHealthData == null)
                return new Dictionary<string, string>();
            else
                return _getHealthData();
        }

        public void Dispose()
        {
            Deactivate();
        }
    }
}
