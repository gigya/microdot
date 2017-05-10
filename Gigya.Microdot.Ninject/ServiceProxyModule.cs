using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.ServiceProxy.Caching;

using Ninject;
using Ninject.Activation;
using Ninject.Infrastructure;
using Ninject.Modules;
using Ninject.Parameters;
using Ninject.Planning.Bindings;
using Ninject.Planning.Bindings.Resolvers;

namespace Gigya.Microdot.Ninject
{
    // Bind service interfaces to ServiceProxy upon request.
    public class ServiceProxyModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Components.Add<IMissingBindingResolver, ServiceProxyBindingResolver>();
        }
    }



    public sealed class ServiceProxyBindingResolver : IMissingBindingResolver
    {
        public INinjectSettings Settings { get; set; }


        public IEnumerable<IBinding> Resolve(Multimap<Type, IBinding> bindings, IRequest request)
        {
            var service = request.Service;

            if (service.IsInterface && service.GetCustomAttribute<HttpServiceAttribute>()!=null)
            {
                return new[]
                {
                    new Binding(service)
                    {
                        ProviderCallback = ctx => new ServiceProxyFactory(service),
                        ScopeCallback = context => StandardScopeCallbacks.Singleton
                    }
                };
            }

            return Enumerable.Empty<IBinding>();
        }


        public void Dispose() { }

    }



    internal class ServiceProxyFactory : IProvider
    {
        public Type Type { get; }


        public ServiceProxyFactory(Type type)
        {
            Type = type;
        }


        public object Create(IContext context)
        {
            var serviceProxyType = typeof(ServiceProxyProvider<>).MakeGenericType(Type);
            var clientProperty = serviceProxyType.GetProperty(nameof(ServiceProxyProvider<int>.Client));
            var serviceProxy = clientProperty.GetValue(context.Kernel.Get(serviceProxyType));

            if (context.Kernel.Get<IMetadataProvider>().HasCachedMethods(Type))
            {
                var cachingProxyType = typeof(CachingProxyProvider<>).MakeGenericType(Type);
                var proxyProperty = cachingProxyType.GetProperty(nameof(CachingProxyProvider<int>.Proxy));
                var cachingProxy = proxyProperty.GetValue(context.Kernel.Get(cachingProxyType,
                    new ConstructorArgument("dataSource", serviceProxy),
                    new ConstructorArgument("serviceName",(string)null)));
                return cachingProxy;
            }

            return serviceProxy;
        }
    }
}
