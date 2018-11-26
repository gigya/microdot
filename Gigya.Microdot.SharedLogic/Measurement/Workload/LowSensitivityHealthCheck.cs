using System;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Metrics;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    /// <summary>
    /// Health check which fails only if it is unhealthy along a period of time
    /// </summary>
    public class LowSensitivityHealthCheck
    {
        private readonly Func<HealthCheckResult> _healthCheck;
        private readonly Func<TimeSpan> _getMinimumUnhealthyDuration;

        private DateTimeOffset _lastHealthyTime;

        private IDateTime DateTime { get; }


        /// <param name="healthCheck">A function to calculate whether the current state is healthy or not</param>
        /// <param name="getMinimumUnhealthyDuration">A function which returns the minimum period to report healthy status although current status is not healthy. The <see cref="GetHealthStatus"/> function will return an unhealthy result only if the health check is unhealthy for more time than the healthiness period.</param>
        public LowSensitivityHealthCheck(Func<HealthCheckResult> healthCheck, Func<TimeSpan> getMinimumUnhealthyDuration, IDateTime dateTime)
        {
            DateTime = dateTime;
            _healthCheck = healthCheck;
            _getMinimumUnhealthyDuration = getMinimumUnhealthyDuration;
            _lastHealthyTime = DateTime.UtcNow;
        }


        public HealthCheckResult GetHealthStatus()
        {
            var result = _healthCheck.Invoke();
            if (result.IsHealthy)
            {
                _lastHealthyTime = DateTime.UtcNow;
                return result;
            }

            var unhealthyStateDuration = DateTime.UtcNow - _lastHealthyTime;
            var healthinessDuration = _getMinimumUnhealthyDuration();
            if (unhealthyStateDuration <= healthinessDuration)
                return HealthCheckResult.Healthy(result.Message + $" (for less than {healthinessDuration})");

            return result;
        }
    }
}
