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
        /// Time to wait for http response from Consul.
        /// When LongPolling=true,  defines the maximum time to wait on long-polling.
        /// When LongPolling=false, defines the timeout for Consul http requests.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Interval for retrying access to Consul after an error has occured
        /// </summary>
        public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
    }
}