using System;
using Microsoft.Extensions.DependencyInjection;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Create new scope
    /// </summary>
    internal class MicrodotServiceScopeFactory : IServiceScopeFactory
    {
        private readonly Func<MicrodotServiceProviderWithScope> _func;

        public MicrodotServiceScopeFactory(Func<MicrodotServiceProviderWithScope> func)
        {
            _func = func;
        }

        public IServiceScope CreateScope()
        {
            return _func();
        }
    }
}