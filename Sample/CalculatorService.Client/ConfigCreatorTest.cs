using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;

namespace CalculatorService.Client
{
    public abstract class ConfigCreatorTestBase<T> where T : class
    {
        protected readonly T _config;

        protected ConfigCreatorTestBase(T config)
        {
            _config = config;
        }

        public virtual T GetConfig()
        {
            return _config;
        }
    }

    public class ConfigCreatorTestObject : ConfigCreatorTestBase<DiscoveryConfig>
    {
        public ConfigCreatorTestObject(DiscoveryConfig config) : base(config)
        { }
    }

    public class ConfigCreatorTestFuncObject : ConfigCreatorTestBase<Func<DiscoveryConfig>>
    {
        public ConfigCreatorTestFuncObject(Func<DiscoveryConfig> config) : base(config)
        { }
    }

    public class ConfigCreatorTestISourceBlockObject : ConfigCreatorTestBase<ISourceBlock<DiscoveryConfig>>
    {
        public ConfigCreatorTestISourceBlockObject(ISourceBlock<DiscoveryConfig> config) : base(config)
        { }
    }

    public class ConfigCreatorTestFuncISourceBlockObject : ConfigCreatorTestBase<Func<ISourceBlock<DiscoveryConfig>>>
    {
        public ConfigCreatorTestFuncISourceBlockObject(Func<ISourceBlock<DiscoveryConfig>> config) : base(config)
        { }
    }
}
