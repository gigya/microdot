using System;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    [Serializable]
    [ConfigurationRoot("ConsulDiscovery", RootStrategy.ReplaceClassNameWithPath)]
    public class ConsulDiscoveryConfig : IConfigObject
    {
        /// <summary>
        /// Whether to Call Consul with long-polling, waiting for changes to occur, or to call it periodically
        /// </summary>
        public bool UseLongPolling { get; set; } = false;

        /// <summary>
        /// Interval for reloading endpoints from Consul, 
        /// Used for a source that is reloading endpoints over and over all time (e.g. ConsulQuery source)
        /// </summary>
        public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Time to wait for response from Consul.
        /// </summary>
        public TimeSpan ReloadTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Interval for retrying acces to Consul in case source is undefined (e.g. Service is not deployed)
        /// </summary>
        public TimeSpan UndefinedRetryInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Interval for retrying access to surce (e.g. Consul) after an error has occured
        /// </summary>
        public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromSeconds(1);
    }
}