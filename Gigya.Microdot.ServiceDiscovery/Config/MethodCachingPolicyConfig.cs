using System;
using System.Runtime.Serialization;

namespace Gigya.Microdot.ServiceDiscovery.Config
{

    /// <summary>
    /// Caching Configuration for specific method on a specific service. Used by CachingProxy.
    /// </summary>
    [Serializable]
    public class MethodCachingPolicyConfig: IMethodCachingSettings
    {
        /// <summary>
        /// Specifies whether caching is enabled for this service method
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// The amount of time after which a request of an item triggers a background refresh from the data source.
        /// </summary>
        public TimeSpan? RefreshTime { get; set; }

        /// <summary>
        /// Specifies the max period time for data to be kept in the cache before it is removed. Successful refreshes
        /// extend the time.
        /// </summary>
        public TimeSpan? ExpirationTime { get; set; }

        /// <summary>
        /// The amount of time to wait before attempting another refresh after the previous refresh failed.
        /// </summary>
        public TimeSpan? FailedRefreshDelay { get; set; }
        public ResponseKinds ResponseKindsToCache { get; set; }
        public ResponseKinds ResponseKindsToIgnore { get; set; }
        public RefreshMode RefreshMode { get; set; }
        public double RefreshTimeInMinutes { get; set; } = -1;
        public double ExpirationTimeInMinutes { get; set; } = -1;
        public double FailedRefreshDelayInSeconds { get; set; } = -1;
        public bool UseRequestGrouping { get; set; } = true;
        public RefreshBehavior RefreshBehavior { get; set; }
        public RevokedResponseBehavior RevokedResponseBehavior { get; set; }
        public ExpirationBehavior ExpirationBehavior { get; set; }
        public bool CacheResponsesWhenCacheWasSupressed { get; set; } = true;

        public MethodCachingPolicyConfig() { OnDeserialized(default(StreamingContext)); }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (RefreshTimeInMinutes != -1)
                RefreshTime = TimeSpan.FromMinutes(RefreshTimeInMinutes);
            if (ExpirationTimeInMinutes != -1)
                ExpirationTime = TimeSpan.FromMinutes(ExpirationTimeInMinutes);
            if (FailedRefreshDelayInSeconds != -1)
                FailedRefreshDelay = TimeSpan.FromSeconds(FailedRefreshDelayInSeconds);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            MethodCachingPolicyConfig other = obj as MethodCachingPolicyConfig;

            if (other == null)
                return false;

            return    Enabled                             == other.Enabled
                   && ResponseKindsToCache                == other.ResponseKindsToCache
                   && ResponseKindsToIgnore               == other.ResponseKindsToIgnore
                   && RefreshMode                         == other.RefreshMode
                   && RefreshTimeInMinutes                == other.RefreshTimeInMinutes
                   && RefreshTime                         == other.RefreshTime
                   && ExpirationTimeInMinutes             == other.ExpirationTimeInMinutes
                   && ExpirationTime                      == other.ExpirationTime
                   && FailedRefreshDelayInSeconds         == other.FailedRefreshDelayInSeconds
                   && FailedRefreshDelay                  == other.FailedRefreshDelay
                   && UseRequestGrouping                  == other.UseRequestGrouping
                   && RefreshBehavior                     == other.RefreshBehavior
                   && RevokedResponseBehavior             == other.RevokedResponseBehavior
                   && ExpirationBehavior                  == other.ExpirationBehavior
                   && CacheResponsesWhenCacheWasSupressed == other.CacheResponsesWhenCacheWasSupressed;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ ResponseKindsToCache.GetHashCode();
                hashCode = (hashCode * 397) ^ ResponseKindsToIgnore.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshMode.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshTimeInMinutes.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshTime.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpirationTimeInMinutes.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpirationTime.GetHashCode();
                hashCode = (hashCode * 397) ^ FailedRefreshDelayInSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ FailedRefreshDelay.GetHashCode();
                hashCode = (hashCode * 397) ^ UseRequestGrouping.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ RevokedResponseBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpirationBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ CacheResponsesWhenCacheWasSupressed.GetHashCode();
                return hashCode;
            }
        }

        public static void Merge(IMethodCachingSettings source, IMethodCachingSettings target)
        {
            if (target.ResponseKindsToCache == 0)                    target.ResponseKindsToCache = source.ResponseKindsToCache;
            if (target.ResponseKindsToIgnore == 0)                   target.ResponseKindsToIgnore = source.ResponseKindsToIgnore;
            if (target.RefreshMode == 0)                             target.RefreshMode = source.RefreshMode;
            if (target.RefreshTimeInMinutes == -1)                 { target.RefreshTimeInMinutes = source.RefreshTimeInMinutes; target.RefreshTime = source.RefreshTime; }
            if (target.ExpirationTimeInMinutes == -1)              { target.ExpirationTimeInMinutes = source.ExpirationTimeInMinutes; target.ExpirationTime = source.ExpirationTime; }
            if (target.FailedRefreshDelayInSeconds == -1)          { target.FailedRefreshDelayInSeconds = source.FailedRefreshDelayInSeconds; target.FailedRefreshDelay = source.FailedRefreshDelay; }
            if (target.UseRequestGrouping == true)                   target.UseRequestGrouping = source.UseRequestGrouping;
            if (target.RefreshBehavior == 0)                         target.RefreshBehavior = source.RefreshBehavior;
            if (target.RevokedResponseBehavior == 0)                 target.RevokedResponseBehavior = source.RevokedResponseBehavior;
            if (target.ExpirationBehavior == 0)                      target.ExpirationBehavior = source.ExpirationBehavior;
            if (target.CacheResponsesWhenCacheWasSupressed == true)  target.CacheResponsesWhenCacheWasSupressed = source.CacheResponsesWhenCacheWasSupressed;
        }
                
    }
}