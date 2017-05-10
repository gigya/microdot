namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    /// <summary>
    /// Result of a status check
    /// </summary>
    public class HealthStatusResult
    {
        /// <summary>
        /// True if the check was successful, false if the check failed.
        /// </summary>
        public bool IsHealthy { get; }

        /// <summary>
        /// Status message of the check. A status can be provided for both healthy and unhealthy states.
        /// </summary>
        public string Message { get; }


        public HealthStatusResult(string message, bool isHealthy=true )
        {
            IsHealthy = isHealthy;
            if (string.IsNullOrWhiteSpace(message))
                message = "FAILED";
            Message = message;
        }
    }
}