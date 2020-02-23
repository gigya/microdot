using System;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    internal class MicrodotServiceProvider : IServiceProvider
    {
        private readonly IResolutionRoot _resolutionRoot;

        public MicrodotServiceProvider(IResolutionRoot resolutionRoot)
        {
            _resolutionRoot = resolutionRoot;
        }

        public object GetService(Type serviceType)
        {
            return _resolutionRoot.Get(serviceType);
        }
    }
}