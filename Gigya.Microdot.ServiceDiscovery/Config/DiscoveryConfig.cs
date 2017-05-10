using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// Config object read from <configuration.Discovery/> XML entry. Will be recreated when config changes.
    /// </summary>
    [Serializable]
    [ConfigurationRoot("Discovery", RootStrategy.ReplaceClassNameWithPath)]
    public class DiscoveryConfig : IConfigObject
    {
        internal ServiceDiscoveryConfig DefaultItem { get; private set; }

        /// <summary>
        /// Scope where this service is installed.
        /// Some services are installed for current environment only (itg1, prod, etc.)
        /// Other services are installed for entire data-center (e.g. Kafka, Flume, etc.)
        /// </summary>
        public ServiceScope Scope { get; set; } = ServiceScope.Environment;

        /// <summary>
        /// The time-out trying to send requests to the service.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// Interval for reloading endpoints from source (e.g. Consul), in Milliseconds
        /// </summary>
        public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// When we lose connection to some endpoint, we wait this delay till we start trying to reconnect.
        /// </summary>
        public double FirstAttemptDelaySeconds { get; set; } = 0.001;

        /// <summary>
        /// When retrying to reconnect to an endpoint, we use exponential backoff (e.g. 1,2,4,8ms, etc). Once that
        /// backoff reaches this value, it won't increase any more.
        /// </summary>
        public double MaxAttemptDelaySeconds { get; set; } = 10;

        /// <summary>
        /// The factor of the exponential backoff when retrying connections to endpoints.
        /// </summary>
        public double DelayMultiplier { get; set; } = 2;

        /// <summary>
        /// The discovery mode to use, e.g. whether to use DNS resolving, Consul, etc.
        /// </summary>
        public DiscoverySource Source { get; set; } = DiscoverySource.Consul;

        /// <summary>
        /// The discovery configuration for the various services.
        /// </summary>
        public IImmutableDictionary<string, ServiceDiscoveryConfig> Services { get; set; }  // <service name, discovery params>

        [Required]
        public PortAllocationConfig PortAllocation { get; set; } = new PortAllocationConfig();

        public bool EnvironmentFallbackEnabled { get; set; } = false;


        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            DefaultItem = new ServiceDiscoveryConfig
            {
                ReloadInterval = ReloadInterval,
                DelayMultiplier = DelayMultiplier,
                FirstAttemptDelaySeconds = FirstAttemptDelaySeconds,
                MaxAttemptDelaySeconds = MaxAttemptDelaySeconds,
                RequestTimeout = RequestTimeout,
                Scope = Scope,
                Source = Source
            };

            var services = (IDictionary<string, ServiceDiscoveryConfig>)Services??new Dictionary<string, ServiceDiscoveryConfig>();
            
            Services = new ServiceDiscoveryCollection(services, DefaultItem, PortAllocation);
        }
    }
}