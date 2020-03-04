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
    public class MicrodotNinectPipline : IPipeline
    {

        IPipeline _pipeline;
        MicrodotServiceProvider GlobalScope;
        private IRequestScopedType requestScopedType;
        private object _locker = new object();
        public MicrodotNinectPipline(IEnumerable<IActivationStrategy> strategies, IActivationCache activationCache)
        {
            _pipeline = new Pipeline(strategies, activationCache);
        }

        public IList<IActivationStrategy> Strategies => _pipeline.Strategies;
        public INinjectSettings Settings { get => _pipeline.Settings; set => _pipeline.Settings = value; }
        public IPipeline Pipeline => this;

        public void Activate(IContext context, InstanceReference reference)
        {
            EnsureGlobalContextExists(context);

            MicrodotNinjectScopParameter scope =
                context
                .Parameters
                .OfType<MicrodotNinjectScopParameter>()
                .LastOrDefault()
                ?? GlobalScope._microdotNinectScopParameter;

            var key = context.Request.Service;
            if (key == typeof(IServiceProvider))
            {
                scope.ServiceProvider.TryGetTarget(out var s);
                reference.Instance = s;
                return;
            }

            if (!requestScopedType.Contains(key))
            {
                _pipeline.Activate(context, reference);
                return;
            }

            //Hendle the lock inside in the chace item scope
            reference.Instance = scope.GetORCreate(key, () =>
                {
                    _pipeline.Activate(context, reference);
                    return reference.Instance;
                });

        }

        private void EnsureGlobalContextExists(IContext context)
        {
            if (GlobalScope == null)
            {
                lock (_locker)
                {
                    if (GlobalScope == null)
                    {
                        requestScopedType = context.Kernel.Get<IRequestScopedType>();
                        var globalServiceProvider = new MicrodotServiceProvider(context.Kernel);
                        GlobalScope = globalServiceProvider;
                    }
                }
            }
        }

        public void Deactivate(IContext context, InstanceReference reference)
        {
            _pipeline.Activate(context, reference);
        }

        public void Dispose()
        {
            _pipeline.Dispose();
        }
    }
}


