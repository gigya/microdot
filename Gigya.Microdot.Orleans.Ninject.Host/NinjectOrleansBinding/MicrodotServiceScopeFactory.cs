using System;
using Microsoft.Extensions.DependencyInjection;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    internal class MicrodotServiceScopeFactory : IServiceScopeFactory
    {
        private readonly Func<MicrodotServiceProvider> _func;

        public MicrodotServiceScopeFactory(Func<MicrodotServiceProvider> func)
        {
            _func = func;
        }

        public IServiceScope CreateScope()
        {
            return _func();
        }
    }
}