using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Ninject;
using Ninject.Activation;
using Ninject.Activation.Caching;
using Ninject.Activation.Strategies;
using Ninject.Injection;
using Ninject.Modules;
using Ninject.Parameters;
using Ninject.Planning;
using Ninject.Planning.Bindings;
using Ninject.Planning.Bindings.Resolvers;
using Ninject.Planning.Strategies;
using Ninject.Planning.Targets;
using Ninject.Selection;
using Ninject.Selection.Heuristics;
using Ninject.Syntax;
using static Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding.CacheItem;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    internal class MicrodotNinjectScopParameter : IParameter
    {

        public MicrodotNinjectScopParameter(CacheItem cache, IServiceProvider serviceProvider)
        {
            Cache = new WeakReference<CacheItem>(cache);
            ServiceProvider = new WeakReference<IServiceProvider>(serviceProvider);
        }

        public string Name => "Scope";

        private WeakReference<CacheItem> Cache { get; }
        public WeakReference<IServiceProvider> ServiceProvider { get; }


        public object GetORCreate(Type key, Func<object>  instanceFunc)
        {
            if (Cache.TryGetTarget(out var chacheItem))
            {
               return chacheItem.GetORCreate(key, instanceFunc);
            }
            throw new Exception("Scope not exists");
        }

        public bool ShouldInherit => true;

        public bool Equals(IParameter other)
        {
            return ReferenceEquals(this, other);
        }

        public object GetValue(IContext context, ITarget target)
        {
            return this;
        }

    }


}
