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
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic;
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
            //Kernel.Components.Add<IBindingResolver, ConfigObjectsBindingResolver>();
            Kernel.Bind<IConfigEventFactory>().To<ConfigEventFactory>();
            Kernel.Bind<IConfigFuncFactory>().ToFactory();
            Kernel.Rebind<IAssemblyProvider>().To<AssemblyProvider>();

            SearchAssembliesAndRebindIConfig(Kernel);
        }

        private void SearchAssembliesAndRebindIConfig(IKernel kernel)
        {
            IAssemblyProvider aProvider = kernel.Get<IAssemblyProvider>();
            foreach (Assembly assembly in aProvider.GetAssemblies())
            {
                foreach (Type configType in assembly.GetTypes().Where(t => !t.IsGenericType && t.IsClass &&
                                                                           t.GetTypeInfo().ImplementedInterfaces.Any(i => i == typeof(IConfigObject))))
                {
                    ConfigObjectCreatorWrapper cocWrapper = new ConfigObjectCreatorWrapper(kernel, configType);

                    dynamic getLataestLambda = GetGenericFuncCompiledLambda(configType, cocWrapper, nameof(ConfigObjectCreatorWrapper.GetTypedLatestFunc));
                    kernel.Rebind(typeof(Func<>).MakeGenericType(configType)).ToMethod(t => getLataestLambda());

                    Type sourceBlockType = typeof(ISourceBlock<>).MakeGenericType(configType);
                    kernel.Rebind(sourceBlockType).ToMethod(m => cocWrapper.GetChangeNotifications());

                    dynamic changeNotificationsLambda = GetGenericFuncCompiledLambda(sourceBlockType, cocWrapper, nameof(ConfigObjectCreatorWrapper.GetChangeNotificationsFunc));
                    kernel.Rebind(typeof(Func<>).MakeGenericType(sourceBlockType)).ToMethod(i => changeNotificationsLambda());

                    kernel.Rebind(configType).ToMethod(i => cocWrapper.GetLatest());
                }
            }
        }

        private dynamic GetGenericFuncCompiledLambda(Type configType, ConfigObjectCreatorWrapper cocWrapper, string functionName)
        {
            MethodInfo func = typeof(ConfigObjectCreatorWrapper).GetMethod(functionName).MakeGenericMethod(configType);
            Expression instance = Expression.Constant(cocWrapper);
            Expression callMethod = Expression.Call(instance, func);
            Type delegateType = typeof(Func<>).MakeGenericType(configType);
            Type parentExpressionType = typeof(Func<>).MakeGenericType(delegateType);

            dynamic lambda = Expression.Lambda(parentExpressionType, callMethod).Compile();

            return lambda;
        }
    }

    //public sealed class ConfigObjectsBindingResolver : IBindingResolver
    //{
    //    private ConcurrentDictionary<Type, ConfigObjectProvider> ProviderPerType { get; } = new ConcurrentDictionary<Type, ConfigObjectProvider>();

    //    public INinjectSettings Settings { get; set; }

    //    public IEnumerable<IBinding> Resolve(Multimap<Type, IBinding> bindings, Type service)
    //    {
    //        Type configObjectType;
    //        if (ConfigObjectProvider.IsConfigObject(service))
    //            configObjectType = service;
    //        else if (ConfigObjectProvider.IsSourceBlock(service) || ConfigObjectProvider.IsConfigObjectWrapper(service))
    //            configObjectType = service.GetGenericArguments().Single();
    //        else
    //            return Enumerable.Empty<IBinding>();         

    //        var provider = ProviderPerType.GetOrAdd(configObjectType, x => new ConfigObjectProvider());

    //        return new[]
    //        {
    //            new Binding(service)
    //            {
    //                ProviderCallback = ctx => provider,
    //                ScopeCallback = StandardScopeCallbacks.Transient
    //            }
    //        };
    //    }

    //    public void Dispose() {  }
    //}


    //public class ConfigObjectProvider : IProvider
    //{        
    //    public Type Type => typeof(IConfigObject);
    //    private ConfigObjectCreator Creator { get; set; }

    //    private readonly object _creatorLock;

    //    private Type _sourceBlockType;

    //    public ConfigObjectProvider()
    //    {
    //        _creatorLock = new object();
    //    }


    //    public object Create(IContext context)
    //    {
    //        var service = context.Request.Service;
    //        if (service == _sourceBlockType)
    //            return GetSourceBlock(context);

    //        if (_sourceBlockType==null && IsSourceBlock(service))
    //        {
    //            _sourceBlockType = service;
    //            return GetSourceBlock(context);
    //        }

    //        if (IsConfigObjectWrapper(service))
    //        {
    //            IConfigObjectWrapper wr = Activator.CreateInstance(service) as IConfigObjectWrapper;
    //            wr.LatestConfig = GetCreator(context).GetLatest;

    //            return wr;
    //        }

    //        return GetCreator(context).GetLatest();
    //    }

    //    private object GetSourceBlock(IContext context)
    //    {
    //        return GetCreator(context).ChangeNotifications;
    //    }

    //    private ConfigObjectCreator GetCreator(IContext context)
    //    {
    //        if (Creator == null)
    //        {
    //            var getCreator = context.Kernel.Get<Func<Type, ConfigObjectCreator>>();
    //            var service = context.Request.Service;
    //            var uninitializedCreator = getCreator(IsSourceBlock(service) || IsConfigObjectWrapper(service) ? service.GetGenericArguments().Single() : service);

    //            lock (_creatorLock)
    //            {
    //                if (Creator == null)
    //                {
    //                    uninitializedCreator.Init();
    //                    Creator = uninitializedCreator;
    //                }
    //            }
    //        }
    //        return Creator;
    //    }

    //    internal static bool IsConfigObject(Type service)
    //    {
    //        return service.IsClass && service.IsAbstract == false && typeof(IConfigObject).IsAssignableFrom(service);
    //    }

    //    internal static bool IsSourceBlock(Type service)
    //    {
    //        return 
    //            service.IsGenericType && 
    //            service.GetGenericTypeDefinition() == typeof(ISourceBlock<>) &&
    //            IsConfigObject(service.GetGenericArguments().Single());
    //    }

    //    internal static bool IsConfigObjectWrapper(Type service)
    //    {
    //        return
    //            service.IsGenericType &&
    //            service.GetGenericTypeDefinition() == typeof(ConfigObjectWrapper<>) &&
    //            IsConfigObject(service.GetGenericArguments().Single());
    //    }
    //}
}
