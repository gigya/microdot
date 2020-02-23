using System;
using Microsoft.Extensions.DependencyInjection;

namespace Gigya.Microdot.Orleans.Ninject.Host.IOC
{
    internal class MicroDotServiceScopeFactory : IServiceScopeFactory
    {
        private readonly Func<MicroDotServiceScope> _func;

        public MicroDotServiceScopeFactory(Func<MicroDotServiceScope> func)
        {
            _func = func;
        }

        public IServiceScope CreateScope()
        {
            return _func();
        }
    }
}