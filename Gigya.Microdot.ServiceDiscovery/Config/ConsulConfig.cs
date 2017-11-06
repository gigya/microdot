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
        public bool UseLongPolling { get; set; } = false;

        /// <summary>
        /// Interval for reloading endpoints from Consul, 
        /// Used only when UseLongPolling=false
        /// </summary>
        public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Time to wait for http response from Consul.
        /// When UseLongPolling=true,  defines the maximum time to wait on long-polling.
        /// When UseLongPolling=false, defines the timeout for Consul http requests.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Interval for retrying access to surce (e.g. Consul) after an error has occured
        /// </summary>
        public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
    }
}