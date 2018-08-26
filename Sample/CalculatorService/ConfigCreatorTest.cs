using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CalculatorService.Interface;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;

namespace CalculatorService
{
    public class ConfigCreatorTest
    {
        //private readonly IConfiguration _config;
        private readonly Func<CacheConfig> _config;
        private readonly Func<DiscoveryConfig> _disCoveryConfig;
        private readonly DiscoveryConfig _discConfig;
        private readonly IConfigEventFactory _configEventFactory;
        private readonly ISourceBlock<MyConfig> _myConfigSource;
        private readonly Func<ISourceBlock<CacheConfig>> _cacheConfigSource;

        public ConfigCreatorTest(Func<CacheConfig> config, ISourceBlock<MyConfig> myConfigSource, Func<ISourceBlock<CacheConfig>> cacheConfigSource, Func<DiscoveryConfig> discoveryConfig, DiscoveryConfig discConfig, IConfigEventFactory configFactory)
        {
            _config = config;
            _myConfigSource = myConfigSource;
            _cacheConfigSource = cacheConfigSource;
            _disCoveryConfig = discoveryConfig;
            _discConfig = discConfig;
            _configEventFactory = configFactory;
        }
        //public ConfigCreatorTest(Func<CacheConfig> config)
        //{
        //    _config = config;
        //}

        public CacheConfig GetConfig()
        {
            //return _config.GetObject<CacheConfig>();
            return _config();
        }

        public DiscoveryConfig GetDiscConfig()
        {
            //return _config.GetObject<CacheConfig>();
            return _discConfig;
        }

        public ISourceBlock<CacheConfig> GetISourceBlockByFunc()
        {
            return _cacheConfigSource();
        }

        public ISourceBlock<MyConfig> GetISourceBlockDirect()
        {
            return _myConfigSource;
        }

        public ISourceBlock<MyConfig> GetISourceBlockByFactory()
        {
            return _configEventFactory.GetChangeEvent<MyConfig>();
        }

        public DiscoveryConfig GetDiscoveryConfig()
        {
            return _disCoveryConfig();
        }
    }
}
