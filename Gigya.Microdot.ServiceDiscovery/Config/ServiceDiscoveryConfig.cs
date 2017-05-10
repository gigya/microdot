using System;

using Gigya.Common.Contracts.HttpService;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// The discovery configuration for a specific service.
    /// </summary>
    [Serializable]
    public class ServiceDiscoveryConfig
    {


        public bool SupportsFallback => Source == DiscoverySource.Consul && Scope == ServiceScope.Environment;

        /// <summary>
        /// Scope where this service is installed.
        /// Some services are installed for current environment only (itg1, prod, etc.)
        /// Other services are installed for entire data-center (e.g. Kafka, Flume, etc.)
        /// </summary>
        public ServiceScope? Scope { get; set; }

        /// <summary>
        /// The time-out trying to send requests to the service.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// When we lose connection to some endpoint, we wait this delay till we start trying to reconnect.
        /// </summary>
        public double? FirstAttemptDelaySeconds { get; set; }

        /// <summary>
        /// When retrying to reconnect to an endpoint, we use exponential backoff (e.g. 1,2,4,8ms, etc). Once that
        /// backoff reaches this value, it won't increase any more.
        /// </summary>
        public double? MaxAttemptDelaySeconds { get; set; }

        /// <summary>
        /// The factor of the exponential backoff when retrying connections to endpoints.
        /// </summary>
        public double? DelayMultiplier { get; set; }

        /// <summary>
        /// The discovery mode to use, e.g. whether to use DNS resolving, Consul, etc.
        /// </summary>
        public DiscoverySource? Source { get; set; }

        /// <summary>
        /// Interval for reloading endpoints from source (e.g. Consul), in Milliseconds
        /// </summary>
        public TimeSpan? ReloadInterval { get; set; }

        public string Hosts { get; set; }

        /// <summary>
        /// Port of service, to be used in case port is not set by any other method, like Consul or service attribute
        /// </summary>
        public int? DefaultPort { get; set; }

        public int? DefaultSlotNumber { get; set; }

        /// <summary>
        /// Get a value indicating if a secure will be used to connect to the remote service. This defaults to the
        /// value that was specified in the <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>, and can
        /// be overridden by discovery configuration.
        /// </summary>
        public bool? UseHttpsOverride { get; set; }

        /// <summary>
        /// Gets or sets the name of server certificate to trust. Defaults to null, which means it will trust a
        /// certificate with any name (but still checks its Certificate Authority).
        /// </summary>
        public string SecurityRole { get; set; }

        public CachingPolicyConfig CachingPolicy { get; set; }

        public TimeSpan? SuppressHealthCheckAfterServiceUnused { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;

            var other = (ServiceDiscoveryConfig)obj;
            return Scope == other.Scope &&
                   RequestTimeout.Equals(other.RequestTimeout) &&
                   FirstAttemptDelaySeconds.Equals(other.FirstAttemptDelaySeconds) &&
                   MaxAttemptDelaySeconds.Equals(other.MaxAttemptDelaySeconds) &&
                   DelayMultiplier.Equals(other.DelayMultiplier) &&
                   Source == other.Source &&
                   ReloadInterval.Equals(other.ReloadInterval) &&
                   string.Equals(Hosts, other.Hosts) &&
                   DefaultPort == other.DefaultPort &&
                   DefaultSlotNumber == other.DefaultSlotNumber &&
                   UseHttpsOverride == other.UseHttpsOverride &&
                   string.Equals(SecurityRole, other.SecurityRole) &&
                   Equals(CachingPolicy, other.CachingPolicy) &&
                   SuppressHealthCheckAfterServiceUnused.Equals(other.SuppressHealthCheckAfterServiceUnused);
        }


        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ RequestTimeout.GetHashCode();
                hashCode = (hashCode * 397) ^ FirstAttemptDelaySeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxAttemptDelaySeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ DelayMultiplier.GetHashCode();
                hashCode = (hashCode * 397) ^ Source.GetHashCode();
                hashCode = (hashCode * 397) ^ ReloadInterval.GetHashCode();
                hashCode = (hashCode * 397) ^ (Hosts != null ? Hosts.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ DefaultPort.GetHashCode();
                hashCode = (hashCode * 397) ^ DefaultSlotNumber.GetHashCode();
                hashCode = (hashCode * 397) ^ UseHttpsOverride.GetHashCode();
                hashCode = (hashCode * 397) ^ (SecurityRole != null ? SecurityRole.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (CachingPolicy != null ? CachingPolicy.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ SuppressHealthCheckAfterServiceUnused.GetHashCode();
                return hashCode;
            }
        }

    }
}