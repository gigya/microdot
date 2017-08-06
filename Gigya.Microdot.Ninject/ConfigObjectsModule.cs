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
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces.Configuration;
using Ninject;
using Ninject.Activation;
using Ninject.Infrastructure;
using Ninject.Modules;
using Ninject.Planning.Bindings;
using Ninject.Planning.Bindings.Resolvers;

namespace Gigya.Microdot.Ninject
{
    public class ConfigObjectsModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.BindPerKey<Type, ConfigObjectCreator>();
            Kernel.Components.Add<IBindingResolver, ConfigObjectsBindingResolver>();
            Kernel.Bind<IConfigEventFactory>().To<ConfigEventFactory>();
        }
    }

    public sealed class ConfigObjectsBindingResolver : IBindingResolver
    {
        public INinjectSettings Settings { get; set; }

        public IEnumerable<IBinding> Resolve(Multimap<Type, IBinding> bindings, Type service)
        {
            ConfigObjectProvider provider;

            if (IsConfigObject(service))
                provider = new ConfigObjectProvider(false);
            else if (service.IsGenericType && service.GetGenericTypeDefinition() == typeof(ISourceBlock<>) &&
                    IsConfigObject(service.GetGenericArguments().Single()) && bindings[service].Any() == false)
                provider = new ConfigObjectProvider(true);
            else
                return Enumerable.Empty<IBinding>();


            return new[]
            {
                new Binding(service)
                {
                    ProviderCallback = ctx => provider,
                    ScopeCallback = StandardScopeCallbacks.Transient
                }
            };
        }


        private bool IsConfigObject(Type type)
        {
            return type.IsClass && type.IsAbstract == false && typeof(IConfigObject).IsAssignableFrom(type);
        }


        public void Dispose() {  }
    }


    public class ConfigObjectProvider : IProvider
    {
        private bool IsBroadcast { get; }
        public Type Type => IsBroadcast ? typeof(ISourceBlock<IConfigObject>) : typeof(IConfigObject);
        private ConfigObjectCreator Creator { get; set; }


        public ConfigObjectProvider(bool isBroadcast)
        {
            IsBroadcast = isBroadcast;
        }


        public object Create(IContext context)
        {
            if (Creator == null)
            {
                var getCreator = context.Kernel.Get<Func<Type, ConfigObjectCreator>>();
                var service = context.Request.Service;
                Creator = getCreator(IsBroadcast ? service.GetGenericArguments().Single() : service);
            }

            return IsBroadcast ? Creator.ChangeNotifications : Creator.GetLatest();
        }
    }
}
