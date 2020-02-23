using System;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Orleans.Ninject.Host.IOC
{
    internal class MicroDotServiceProvider : IServiceProvider
    {
        private readonly IResolutionRoot _resolutionRoot;

        public MicroDotServiceProvider(IResolutionRoot resolutionRoot)
        {
            _resolutionRoot = resolutionRoot;
        }

        public object GetService(Type serviceType)
        {
            return _resolutionRoot.Get(serviceType);
        }
    }
}