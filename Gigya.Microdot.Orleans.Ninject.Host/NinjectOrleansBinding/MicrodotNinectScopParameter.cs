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
using static Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding.ScopeCache;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Use to transfar the scope cahce into ninjet request
    /// </summary>
    internal class MicrodotNinjectScopParameter : IParameter
    {
        public MicrodotNinjectScopParameter(ScopeCache cache, IServiceProvider serviceProvider)
        {
            Cache = cache;
            ServiceProvider = serviceProvider;
        }

        public string Name => "Scope";

        private ScopeCache Cache { get; }
        public IServiceProvider ServiceProvider { get; }


        public object GetORCreate(Type key, Func<object> instanceFunc)
        {
            return Cache.GetORCreate(key, instanceFunc);

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
