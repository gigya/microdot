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
    internal class MicrodotNinectScopParameter : IParameter
    {
        public MicrodotNinectScopParameter(IRequestScopedType requestScoped, CacheItem cache)
        {
            RequestScoped = requestScoped;
            Cache = new WeakReference<CacheItem>(cache);
        }

        public string Name => "Scope";

        private IRequestScopedType RequestScoped { get; }
        private WeakReference<CacheItem> Cache { get; }

        public bool TryGet(Type key, out object instance)
        {
            if (RequestScoped.Contains(key) && Cache.TryGetTarget(out var chacheItem))
            {
                if (chacheItem.TryGet(key, out var result))
                {
                    instance = result;
                    return true;
                }


            }
            instance = null;
            return false;
        }
        public void Put(Type key, object instance)
        {
            if (Cache.TryGetTarget(out var chacheItem))
            {
                chacheItem.Add(key, instance);
            }
        }

        public bool ShouldInherit => false;

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
