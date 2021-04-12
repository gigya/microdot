using System;
using System.Runtime.Serialization;
using Gigya.Common.Contracts.Attributes;

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
        public RequestGroupingBehavior RequestGroupingBehavior { get; set; }
        public RefreshBehavior RefreshBehavior { get; set; }
        public RevokedResponseBehavior RevokedResponseBehavior { get; set; }
        public ExpirationBehavior ExpirationBehavior { get; set; }
        public CacheResponsesWhenSupressedBehavior CacheResponsesWhenSupressedBehavior { get; set; }
        public NotIgnoredResponseBehavior NotIgnoredResponseBehavior { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
        }

        public MethodCachingPolicyConfig() { }

        //Because CachedAttribute Enabled property is not in use and throws we 
        //Create MethodCachingPolicyConfig using CachedAttribute with Enabled
        //Value 'null', so it wont effect the configuration merge
        public MethodCachingPolicyConfig(CachedAttribute attr)
        {
            Enabled                             = null;
            ResponseKindsToCache                = attr.ResponseKindsToCache;
            ResponseKindsToIgnore               = attr.ResponseKindsToIgnore;
            RefreshMode                         = attr.RefreshMode;
            RefreshTime                         = attr.RefreshTime;
            ExpirationTime                      = attr.ExpirationTime;
            FailedRefreshDelay                  = attr.FailedRefreshDelay;
            RequestGroupingBehavior             = attr.RequestGroupingBehavior;
            RefreshBehavior                     = attr.RefreshBehavior;
            RevokedResponseBehavior             = attr.RevokedResponseBehavior;
            ExpirationBehavior                  = attr.ExpirationBehavior;
            CacheResponsesWhenSupressedBehavior = attr.CacheResponsesWhenSupressedBehavior;
            NotIgnoredResponseBehavior          = attr.NotIgnoredResponseBehavior;
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

            return    Enabled                                       == other.Enabled
                   && ResponseKindsToCache                          == other.ResponseKindsToCache
                   && ResponseKindsToIgnore                         == other.ResponseKindsToIgnore
                   && RefreshMode                                   == other.RefreshMode
                   && RefreshTime                                   == other.RefreshTime
                   && ExpirationTime                                == other.ExpirationTime
                   && FailedRefreshDelay                            == other.FailedRefreshDelay
                   && RequestGroupingBehavior                       == other.RequestGroupingBehavior
                   && RefreshBehavior                               == other.RefreshBehavior
                   && RevokedResponseBehavior                       == other.RevokedResponseBehavior
                   && ExpirationBehavior                            == other.ExpirationBehavior
                   && CacheResponsesWhenSupressedBehavior           == other.CacheResponsesWhenSupressedBehavior
                   && NotIgnoredResponseBehavior                    == other.NotIgnoredResponseBehavior;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ ResponseKindsToCache.GetHashCode();
                hashCode = (hashCode * 397) ^ ResponseKindsToIgnore.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshMode.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshTime.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpirationTime.GetHashCode();
                hashCode = (hashCode * 397) ^ FailedRefreshDelay.GetHashCode();
                hashCode = (hashCode * 397) ^ RequestGroupingBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ RefreshBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ RevokedResponseBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ ExpirationBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ CacheResponsesWhenSupressedBehavior.GetHashCode();
                hashCode = (hashCode * 397) ^ NotIgnoredResponseBehavior.GetHashCode();
                return hashCode;
            }
        }

        public static void Merge(IMethodCachingSettings source, IMethodCachingSettings target)
        {
            if (target.Enabled == null)                          target.Enabled = source.Enabled;
            if (target.ResponseKindsToCache == 0)                target.ResponseKindsToCache = source.ResponseKindsToCache;
            if (target.ResponseKindsToIgnore == 0)               target.ResponseKindsToIgnore = source.ResponseKindsToIgnore;
            if (target.RefreshMode == 0)                         target.RefreshMode = source.RefreshMode;
            if (target.RefreshTime == null)                      target.RefreshTime = source.RefreshTime;
            if (target.ExpirationTime == null)                   target.ExpirationTime = source.ExpirationTime;
            if (target.FailedRefreshDelay == null)               target.FailedRefreshDelay = source.FailedRefreshDelay;
            if (target.RequestGroupingBehavior == 0)             target.RequestGroupingBehavior = source.RequestGroupingBehavior;
            if (target.RefreshBehavior == 0)                     target.RefreshBehavior = source.RefreshBehavior;
            if (target.RevokedResponseBehavior == 0)             target.RevokedResponseBehavior = source.RevokedResponseBehavior;
            if (target.ExpirationBehavior == 0)                  target.ExpirationBehavior = source.ExpirationBehavior;
            if (target.CacheResponsesWhenSupressedBehavior == 0) target.CacheResponsesWhenSupressedBehavior = source.CacheResponsesWhenSupressedBehavior;
            if (target.NotIgnoredResponseBehavior == 0)          target.NotIgnoredResponseBehavior = source.NotIgnoredResponseBehavior;
        }
                
    }
}