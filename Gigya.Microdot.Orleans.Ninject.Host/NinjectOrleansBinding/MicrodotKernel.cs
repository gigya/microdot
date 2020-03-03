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
    public class MicrodotKernel : StandardKernel
    {
        protected override IKernel KernelInstance => this;

        public MicrodotKernel(params INinjectModule[] modules)
            : base(modules)
        {
        }

        public MicrodotKernel(INinjectSettings settings, params INinjectModule[] modules)
            : base(settings, modules)
        {
        }

        protected override void AddComponents()
        {
            base.AddComponents();
            base.Components.RemoveAll<IPipeline>();
            base.Components.Add<IPipeline, MicrodotNinectPipline>();

        }
    }

}
