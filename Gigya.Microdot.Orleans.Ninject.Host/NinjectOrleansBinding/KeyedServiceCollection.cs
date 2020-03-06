using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
       public class KeyedServiceCollection<TKey, TService> : IKeyedServiceCollection<TKey, TService>
        where TService : class
    {
        public TService GetService(IServiceProvider services, TKey key)
        {
            return GetServices(services).FirstOrDefault(s => s.Equals(key))?.GetService(services);
        }

        public IEnumerable<IKeyedService<TKey, TService>> GetServices(IServiceProvider services)
        {
            // ninject IEnumerable feature the get all the implementations of service 
            return services.GetService<IEnumerable<IKeyedService<TKey, TService>>>();
        }
    }
}