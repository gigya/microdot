using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Ninject;
using Ninject.Syntax;
using System;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.Ninject
{
    public  class ConfigEventFactory : IConfigEventFactory
    {
        private readonly IResolutionRoot _resolutionRoot;
        private readonly static object _obj = new object();

        public ConfigEventFactory(IResolutionRoot resolutionRoot)
        {
            _resolutionRoot = resolutionRoot;
        }

        public ISourceBlock<T> GetChangeEvent<T>() where T : IConfigObject
        {
            var guid = Guid.NewGuid().ToString("N");
            Console.WriteLine($"*** {nameof(T)} - {guid}");

            ISourceBlock<T> res;
            lock (_obj)
            {
                Console.WriteLine($"*** Inside1 lock on ConfigEventFactory - {nameof(T)} - {guid}");
                res = _resolutionRoot.Get<ISourceBlock<T>>();
                Console.WriteLine($"*** Inside2 lock on ConfigEventFactory - {nameof(T)} - {guid}");
            }
            Console.WriteLine($"*** After lock on ConfigEventFactory - {guid}");
            return res;
        }
    }
}