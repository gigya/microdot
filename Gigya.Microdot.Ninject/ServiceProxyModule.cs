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
                        ScopeCallback = StandardScopeCallbacks.Singleton
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
            var serviceProxyType = typeof(IServiceProxyProvider<>).MakeGenericType(Type);
            var clientProperty = serviceProxyType.GetProperty(nameof(IServiceProxyProvider<int>.Client));
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
