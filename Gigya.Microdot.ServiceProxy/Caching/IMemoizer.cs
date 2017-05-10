using System;
using System.Reflection;
using System.Runtime.Caching;

using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public interface IMemoizer
    {
        object Memoize(object dataSource, MethodInfo method, object[] args, CacheItemPolicyEx policy);
    }



    public class CacheItemPolicyEx : CacheItemPolicy
    {
        /// <summary>
        /// The amount of time after which a request of an item triggers a background refresh from the data source.
        /// </summary>
        public TimeSpan RefreshTime { get; set; }

        /// <summary>
        /// The amount of time to wait before attempting another refresh after the previous refresh failed.
        /// </summary>
        public TimeSpan FailedRefreshDelay { get; set; }


        public CacheItemPolicyEx() { }

        public CacheItemPolicyEx(CachingPolicyConfig config)
        {
            AbsoluteExpiration = DateTime.UtcNow + config.ExpirationTime;
            RefreshTime = config.RefreshTime;
            FailedRefreshDelay = config.FailedRefreshDelay;
        }
    }
}
