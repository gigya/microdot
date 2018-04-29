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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.Orleans.Hosting;
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
        private ConcurrentDictionary<Type, Type> TypeToElementTypeInterface { get; } = new ConcurrentDictionary<Type, Type>();


        public IServiceProvider ConfigureServices(IServiceCollection services)
        {            
            foreach (var descriptor in services)
            {
                IBindingWhenInNamedWithOrOnSyntax<object> binding;

                if (descriptor.ImplementationType != null)
                {
                    binding = Kernel.Rebind(descriptor.ServiceType).To(descriptor.ImplementationType);
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    binding = Kernel.Rebind(descriptor.ServiceType).ToMethod(context => descriptor.ImplementationFactory(this));
                }
                else
                {
                    binding = Kernel.Rebind(descriptor.ServiceType).ToConstant(descriptor.ImplementationInstance);
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

            var globalConfiguration = Kernel.Get<GlobalConfiguration>();
            globalConfiguration.SerializationProviders.Add(typeof(OrleansCustomSerialization).GetTypeInfo());
            //globalConfiguration.SerializationProviders.Add(typeof(RequestExceptionSerialization).GetTypeInfo());
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
            {
                if (Kernel.CanResolve(serviceType) == false && serviceType.Namespace?.StartsWith("Orleans") == true)
                    return null;

                return Kernel.Get(serviceType);
            }


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
            clusterConfiguration.UseStartupType<NinjectOrleansServiceProvider>();
            return clusterConfiguration;
        }

    }
}