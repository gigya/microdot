using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host.IOC
{
    //Idea here is to in reach microsoft abstraction to bind service per key
    //In Ninject we can simplify the default implementation by calling IEnumerable<TService> for multiple implementation
    //We have need for similar solution that is more robust we have a lot of keys for the same service.
    //We implement it by create similar abstraction but register it on a dictionary<Key,TService> you can read more look for BindPerKey
    public class KeyedServiceCollection<TKey, TService> : IKeyedServiceCollection<TKey, TService>
        where TService : class
    {
        public TService GetService(IServiceProvider services, TKey key)
        {
            return GetServices(services).FirstOrDefault(s => s.Equals(key))?.GetService(services);
        }

        public IEnumerable<IKeyedService<TKey, TService>> GetServices(IServiceProvider services)
        {
            return services.GetService<IEnumerable<IKeyedService<TKey, TService>>>();
        }
    }
}