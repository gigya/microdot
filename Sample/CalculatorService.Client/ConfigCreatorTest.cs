using System;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;

namespace CalculatorService.Client
{
    public class ConfigCreatorTest
    {
        //private readonly IConfiguration _config;
        private readonly Func<DiscoveryConfig> _disConfigFunc;
        private readonly DiscoveryConfig _discConfig;
        private readonly IConfigEventFactory _configEventFactory;
        private readonly ISourceBlock<DiscoveryConfig> _discConfigSource;
        private readonly Func<ISourceBlock<DiscoveryConfig>> _discConfigFuncSource;

        //public ConfigCreatorTest(Func<CacheConfig> config, ISourceBlock<MyConfig> myConfigSource, Func<ISourceBlock<CacheConfig>> cacheConfigSource, Func<DiscoveryConfig> discoveryConfig, DiscoveryConfig discConfig, IConfigEventFactory configFactory)
        public ConfigCreatorTest(Func<ISourceBlock<DiscoveryConfig>> discConfig)
        {
            //_config = config;
            //_discConfigSource = discConfig;
            _discConfigFuncSource = discConfig;
            //_disConfigFunc = discConfig;
            //_discConfig = discConfig;
            //_configEventFactory = configFactory;
        }


        //public ConfigCreatorTest(Func<CacheConfig> config)
        //{
        //    _config = config;
        //}

        public DiscoveryConfig GetConfigByFunc()
        {
            //return _config.GetObject<CacheConfig>();
            return _disConfigFunc();
        }

        public DiscoveryConfig GetDiscConfig()
        {
            //return _config.GetObject<CacheConfig>();
            return _discConfig;
        }

        public ISourceBlock<DiscoveryConfig> GetISourceBlockByFunc()
        {
            return _discConfigFuncSource();
        }

        public ISourceBlock<DiscoveryConfig> GetISourceBlockDirect()
        {
            return _discConfigSource;
        }

        public DiscoveryConfig GetDiscoveryConfig()
        {
            return _disConfigFunc();
        }
    }
}
