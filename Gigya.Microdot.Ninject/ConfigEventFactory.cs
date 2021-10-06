using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Ninject;
using Ninject.Syntax;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.Ninject
{
    public  class ConfigEventFactory : IConfigEventFactory
    {
        private readonly IResolutionRoot _resolutionRoot;

        public ConfigEventFactory(IResolutionRoot resolutionRoot)
        {
            _resolutionRoot = resolutionRoot;
        }

        public ISourceBlock<T> GetChangeEvent<T>() where T : IConfigObject
        {
            return _resolutionRoot.Get<ISourceBlock<T>>();
        }
    }
}