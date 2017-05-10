using System;
using System.Reflection;
using System.Reflection.DispatchProxy;

using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;


namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class CachingProxyProvider<TInterface>
    {
        /// <summary>
        /// The instance of the transparent proxy used to access the data source with caching.
        /// </summary>
        /// <remarks>
        /// This is a thread-safe instance.
        /// </remarks>
        public TInterface Proxy { get; }

        /// <summary>
        /// The instance of the actual data source, used when the data is not present in the cache.
        /// </summary>
        public TInterface DataSource { get; }


        private IMemoizer Memoizer { get; }
        private MetadataProvider MetadataProvider { get; }
        private ILog Log { get; }
        private IDateTime DateTime { get; }
        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }
        private string ServiceName { get; }


        public CachingProxyProvider(TInterface dataSource, IMemoizer memoizer, MetadataProvider metadataProvider, Func<DiscoveryConfig> getDiscoveryConfig, ILog log, IDateTime dateTime, string serviceName)
        {
            DataSource = dataSource;
            Memoizer = memoizer;
            MetadataProvider = metadataProvider;
            GetDiscoveryConfig = getDiscoveryConfig;
            Log = log;
            DateTime = dateTime;

            Proxy = DispatchProxy.Create<TInterface, DelegatingDispatchProxy>();
            ((DelegatingDispatchProxy)(object)Proxy).InvokeDelegate = Invoke;
            ServiceName = serviceName ?? typeof(TInterface).GetServiceName();

            var config = GetConfig();

            if (config.Enabled)
            {
                Log.Info(_ => _("Caching has been enabled for an interface.", unencryptedTags: new
                {
                    ServiceName = typeof(TInterface).GetServiceName(),
                    InterfaceName = typeof(TInterface).FullName,
                    config.ExpirationTime,
                    config.RefreshTime
                }));
            }
        }


        private CachingPolicyConfig GetConfig()
        {
            ServiceDiscoveryConfig config;
            GetDiscoveryConfig().Services.TryGetValue(ServiceName, out config);
        
            return config?.CachingPolicy ?? new CachingPolicyConfig();
        }

        private object Invoke(MethodInfo targetMethod, object[] args)
        {
            var config = GetConfig();
            if (!config.Enabled)
                return targetMethod.Invoke(DataSource, args);

            if (MetadataProvider.IsCached(targetMethod))
                return Memoizer.Memoize(DataSource, targetMethod, args, new CacheItemPolicyEx(config));
            else
                return targetMethod.Invoke(DataSource, args);
        }
    }
}
