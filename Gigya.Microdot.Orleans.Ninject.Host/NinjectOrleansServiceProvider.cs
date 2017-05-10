using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using Ninject;
using Ninject.Syntax;

using Orleans.Runtime.Configuration;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    /// <summary>
    /// Used to plug Ninject into Orleans so that grains can use dependency injection (DI).
    /// </summary>
    public class NinjectOrleansServiceProvider : IServiceProvider
    {
        internal static IKernel Kernel { get; set; }
        private ConcurrentDictionary<Type, Type> TypeToElementTypeInterface { get; }= new ConcurrentDictionary<Type, Type>();

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {            
      
            foreach (var descriptor in services)
            {
                IBindingWhenInNamedWithOrOnSyntax<object> binding;

                if (descriptor.ImplementationType != null)
                {
                    binding = Kernel.Bind(descriptor.ServiceType).To(descriptor.ImplementationType);
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    binding = Kernel.Bind(descriptor.ServiceType).ToMethod(context => descriptor.ImplementationFactory(this));
                }
                else
                {
                    binding = Kernel.Bind(descriptor.ServiceType).ToConstant(descriptor.ImplementationInstance);
                }

                switch (descriptor.Lifetime)
                {
                    case ServiceLifetime.Singleton:
                        binding.InSingletonScope();
                        break;
                    case ServiceLifetime.Scoped:
                        throw new NotImplementedException("We do not support Scoped binding of Orleans.");
                    case ServiceLifetime.Transient:
                        binding.InTransientScope();
                        break;
                }
            }

            return this;
        }


        public object GetService(Type serviceType)
        {
            var elementType = TypeToElementTypeInterface.GetOrAdd(serviceType, t =>
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return t.GetGenericArguments().FirstOrDefault();
                else
                    return null;
            });

            if (elementType == null)
                return Kernel.Get(serviceType);

            var results = Kernel.GetAll(elementType).ToArray();
            var typedResults = Array.CreateInstance(elementType, results.Length);
            Array.Copy(results, typedResults, results.Length);
            return typedResults;
        }
    }

    public static class OrleansNinjectExtensions
    {
        public static ClusterConfiguration WithNinject(this ClusterConfiguration clusterConfiguration, IKernel kernel)
        {
            if (NinjectOrleansServiceProvider.Kernel != null)
                throw new InvalidOperationException("NinjectOrleansServiceProvider is already in use.");
            
            NinjectOrleansServiceProvider.Kernel = kernel;
            clusterConfiguration.Defaults.StartupTypeName = typeof(NinjectOrleansServiceProvider).AssemblyQualifiedName;
            return clusterConfiguration;
        }

    }
}