#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Framework.DependencyInjection.Ninject;
using Ninject;
using Ninject.Syntax;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host
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

    /// <summary>
    /// Used to plug Ninject into Orleans so that grains can use dependency injection (DI).
    /// </summary>
    public class OrleansToNinjectBinding : IOrleansToNinjectBinding
    {

        public OrleansToNinjectBinding(IKernel kernel)
        {
            Kernel = kernel;
        }

        internal IKernel Kernel { get; set; }

        public void ConfigureServices(IServiceCollection services)
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
                    binding = Kernel.Bind(descriptor.ServiceType).ToMethod(context =>
                    {
                        var serviceProvider = context.Kernel.Get<IServiceProvider>();
                        return descriptor.ImplementationFactory(serviceProvider);
                    });
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
                        binding.InRequestScope();
                        break;

                    case ServiceLifetime.Transient:
                        binding.InTransientScope();
                        break;
                }
            }

            Kernel.Rebind(typeof(IKeyedServiceCollection<,>)).To(typeof(KeyedServiceCollection<,>));
            Kernel.Rebind(typeof(ILoggerFactory)).To(typeof(NonBlockingLoggerFactory)).InSingletonScope();

            Kernel.Bind<IServiceProvider>().ToMethod(context =>
            {
                var resolver = context.Kernel.Get<IResolutionRoot>();
                var inheritedParams = context.Parameters.Where(p => p.ShouldInherit);

                var scopeParam = new ScopeParameter();
                inheritedParams = inheritedParams.AddOrReplaceScopeParameter(scopeParam);

                return new NinjectServiceProvider(resolver, inheritedParams.ToArray());
            }).InRequestScope();

            Kernel.Bind<IServiceScopeFactory>().ToMethod(context => { return new NinjectServiceScopeFactory(context); })
                .InRequestScope();
        }


    }

    /// <summary>
    /// Replacing the original Microsoft Logger factory to avoid blocking code.
    /// Ninject using lock by scope which leading to deadlock in this scenario.
    /// </summary>
    public class NonBlockingLoggerFactory : ILoggerFactory
    {
        private ILoggerProvider LoggerProvider;
        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return LoggerProvider.CreateLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            LoggerProvider = provider;
        }

        public NonBlockingLoggerFactory(OrleansLogProvider provider)
        {
            LoggerProvider = provider;
        }
    }


    
}