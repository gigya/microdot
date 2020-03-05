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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Gigya.Microdot.Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ninject;
using Ninject.Activation;
using Ninject.Activation.Caching;
using Ninject.Parameters;
using Ninject.Planning.Bindings;
using Ninject.Planning.Targets;
using Ninject.Syntax;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Used to plug Ninject into Orleans so that grains can use dependency injection (DI).
    /// </summary>

    internal class OrleansToNinjectBinding : IOrleansToNinjectBinding
    {
        public OrleansToNinjectBinding(IKernel kernel)
        {
            Kernel = kernel;
        }
        private IKernel Kernel { get; set; }
        public void ConfigureServices(IServiceCollection services)
        {
            DeadlockDetector.validate(services);
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
                        binding.When((r) => r.Parameters.Contains(ResolveRealParameter.instance));
                        Kernel.Bind(descriptor.ServiceType).ToMethod(BindPerScope).InTransientScope();
                        break;

                    case ServiceLifetime.Transient:
                        binding.InTransientScope();
                        break;
                }
            }

            Kernel.Rebind(typeof(IKeyedServiceCollection<,>)).To(typeof(KeyedServiceCollection<,>)).InSingletonScope();
            Kernel.Rebind(typeof(ILoggerFactory)).To(typeof(NonBlockingLoggerFactory)).InSingletonScope();
            Kernel.Bind<IServiceScopeFactory>().To<MicrodotServiceScopeFactory>().InSingletonScope();

            Kernel.Bind<MicrodotServiceProviderWithScope>().ToSelf().InTransientScope();
            Kernel.Bind<IGlobalServiceProvider>().To< MicrodotServiceProviderWithScope>().InSingletonScope();


            //Should 

            //Support inhrit scope 
            Kernel.Bind<IServiceProvider>().ToMethod(context =>
            {
                MicrodotNinjectScopParameter scope =
                   context.Parameters
                    .OfType<MicrodotNinjectScopParameter>()
                    .LastOrDefault();
                bool hasScope = (scope != null);
                if (hasScope) return scope.ServiceProvider;

                return context.Kernel.Get<IGlobalServiceProvider>();
            }).InTransientScope();
        }

        public object BindPerScope(IContext context)
        {
            var key = context.Request.Service;

            MicrodotNinjectScopParameter scope =
            context.Parameters
            .OfType<MicrodotNinjectScopParameter>()
            .LastOrDefault();
            if (scope != null)
            {
                //Hendle the lock inside in the chace item scope
                return scope.GetORCreate(key, () => context.Kernel.Get(key, ResolveRealParameter.instance));
            }
            throw new RequestScopDependencyOnGlobalScopeException($"{key.FullName}");
        }

    }
}