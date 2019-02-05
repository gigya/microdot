using System;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceDiscovery.Config
{

    [Serializable]
    [ConfigurationRoot("Consul", RootStrategy.ReplaceClassNameWithPath)]
    public class ConsulConfig : IConfigObject
    {
        /// <summary>
        /// Whether to Call Consul with long-polling, waiting for changes to occur, or to call it periodically
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public bool LongPolling { get; set; } = false;

        /// <summary>
        /// Interval for reloading endpoints from Consul, 
        /// Used for Consul queries loop
        /// </summary>
        public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Timeout passed to Consul telling it when to break long-polling.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Absout timeout to be added to HttpTaskTimeout
        /// </summary>
        public TimeSpan HttpTimeoutAdditionalDelay { get; set; } = TimeSpan.FromSeconds(6);

        /// <summary>
        /// Time to wait for http response from Consul.
        /// When LongPolling=true,  defines the maximum time to wait on long-polling.
        /// When LongPolling=false, defines the timeout for Consul http requests.
        /// We take a few seconds more than <see cref="HttpTimeout"/> to reduce the
        /// risk of getting task cancelled exceptions before Consul gracefully timed out,
        /// due to network latency or the process being overloaded.
        /// </summary>
        public TimeSpan HttpTaskTimeout => HttpTimeout
            .Add(TimeSpan.FromMilliseconds((HttpTimeout.TotalMilliseconds / 16)))
            .Add(HttpTimeoutAdditionalDelay);

        /// <summary>
        /// Interval for retrying access to Consul after an error has occured
        /// </summary>
        public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
    }
}