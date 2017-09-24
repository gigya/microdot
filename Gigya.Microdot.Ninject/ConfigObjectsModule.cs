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
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces.Configuration;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Factory;
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
            Kernel.Rebind<ConfigObjectCreator>().ToSelf().InTransientScope();
            Kernel.Components.Add<IBindingResolver, ConfigObjectsBindingResolver>();
            Kernel.Bind<IConfigEventFactory>().To<ConfigEventFactory>();
            Kernel.Bind<IConfigFuncFactory>().ToFactory();

        }
    }

    public sealed class ConfigObjectsBindingResolver : IBindingResolver
    {
        private ConcurrentDictionary<Type, ConfigObjectProvider> ProviderPerType { get; } = new ConcurrentDictionary<Type, ConfigObjectProvider>();

        public INinjectSettings Settings { get; set; }

        public IEnumerable<IBinding> Resolve(Multimap<Type, IBinding> bindings, Type service)
        {
            Type configObjectType;
            if (ConfigObjectProvider.IsConfigObject(service))
                configObjectType = service;
            else if (ConfigObjectProvider.IsSourceBlock(service))
                configObjectType = service.GetGenericArguments().Single();
            else
                return Enumerable.Empty<IBinding>();         

            var provider = ProviderPerType.GetOrAdd(configObjectType, x => new ConfigObjectProvider());

            return new[]
            {
                new Binding(service)
                {
                    ProviderCallback = ctx => provider,
                    ScopeCallback = StandardScopeCallbacks.Transient
                }
            };
        }

        public void Dispose() {  }
    }


    public class ConfigObjectProvider : IProvider
    {        
        public Type Type => typeof(IConfigObject);
        private ConfigObjectCreator Creator { get; set; }

        private readonly object _creatorLock;

        private Type _sourceBlockType;

        public ConfigObjectProvider()
        {
            _creatorLock = new object();
        }


        public object Create(IContext context)
        {
            var service = context.Request.Service;
            if (service == _sourceBlockType)
                return GetSourceBlock(context);

            if (_sourceBlockType==null && IsSourceBlock(service))
            {
                _sourceBlockType = service;
                return GetSourceBlock(context);
            }

            return GetCreator(context).GetLatest();
        }

        private object GetSourceBlock(IContext context)
        {
            return GetCreator(context).ChangeNotifications;
        }

        private ConfigObjectCreator GetCreator(IContext context)
        {
            if (Creator == null)
            {
                lock (_creatorLock)
                    if (Creator == null)
                    {
                        var getCreator = context.Kernel.Get<Func<Type, ConfigObjectCreator>>();
                        var service = context.Request.Service;
                        Creator = getCreator(IsSourceBlock(service) ? service.GetGenericArguments().Single() : service);
                    }
            }
            return Creator;
        }

        internal static bool IsConfigObject(Type service)
        {
            return service.IsClass && service.IsAbstract == false && typeof(IConfigObject).IsAssignableFrom(service);
        }

        internal static bool IsSourceBlock(Type service)
        {
            return 
                service.IsGenericType && 
                service.GetGenericTypeDefinition() == typeof(ISourceBlock<>) &&
                IsConfigObject(service.GetGenericArguments().Single());
        }

    }
}
