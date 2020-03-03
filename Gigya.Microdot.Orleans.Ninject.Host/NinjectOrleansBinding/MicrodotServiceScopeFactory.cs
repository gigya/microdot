using System;
using Microsoft.Extensions.DependencyInjection;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    internal class MicrodotServiceScopeFactory : IServiceScopeFactory
    {
        private readonly Func<NinjectServiceProvider> _func;

        public MicrodotServiceScopeFactory(Func<NinjectServiceProvider> func)
        {
            _func = func;
        }

        public IServiceScope CreateScope()
        {
            return _func();
        }
    }
}