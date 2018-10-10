using System;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.UnitTests.Configuration.Benchmark
{
    public abstract class ConfigCreatorBase<T> where T : class 
    {
        protected readonly T _config;

        protected ConfigCreatorBase(T config)
        {
            _config = config;
        }

        public virtual T GetConfig()
        {
            return _config;
        }
    }

    public class ConfigCreatorObject : ConfigCreatorBase<DiscoveryConfig>
    {
        public ConfigCreatorObject(DiscoveryConfig config) : base(config)
        {}
    }

    public class ConfigCreatorFuncObject : ConfigCreatorBase<Func<DiscoveryConfig>>
    {
        public ConfigCreatorFuncObject(Func<DiscoveryConfig> config) : base(config)
        {}
    }

    public class ConfigCreatorISourceBlockObject : ConfigCreatorBase<ISourceBlock<DiscoveryConfig>>
    {
        public ConfigCreatorISourceBlockObject(ISourceBlock<DiscoveryConfig> config) : base(config)
        {}
    }

    public class ConfigCreatorFuncISourceBlockObject : ConfigCreatorBase<Func<ISourceBlock<DiscoveryConfig>>>
    {
        public ConfigCreatorFuncISourceBlockObject(Func<ISourceBlock<DiscoveryConfig>> config) : base(config)
        {}
    }
}
