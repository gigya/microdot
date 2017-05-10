using System;

using Metrics;
using Metrics.Core;

namespace Gigya.Microdot.Configuration
{
    public class ConfigurationHealthCheck : HealthCheck
    {
        private bool _isHealthy;
        private Exception _lastError;

        public ConfigurationHealthCheck()
            :base("Configuration")
        {
            HealthChecks.RegisterHealthCheck(this);            
        }

        public void Healthy()
        {
            _isHealthy = true;
        }
        public void Unhealthy(Exception ex)
        {
            _lastError = ex;
            _isHealthy = false;
        }

        protected override HealthCheckResult Check()
        {
            return _isHealthy
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(_lastError);
        }
    }
}