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
        public MicrodotNinectPipline(IEnumerable<IActivationStrategy> strategies, IActivationCache activationCache)
        {
            _pipeline = new Pipeline(strategies, activationCache);
        }

        public IList<IActivationStrategy> Strategies => _pipeline.Strategies;
        public INinjectSettings Settings { get => _pipeline.Settings; set => _pipeline.Settings = value; }
        public IPipeline Pipeline => this;

        public void Activate(IContext context, InstanceReference reference)
        {
            MicrodotNinectScopParameter scope =
                context
                .Parameters
                .OfType<MicrodotNinectScopParameter>()
                .LastOrDefault();

            var key = context.Request.Service;

            if (scope != null && scope.TryGet(key, out var r))
            {
                reference.Instance = r;
            }
            else
            {
                _pipeline.Activate(context, reference);
                scope?.Put(key, reference.Instance);
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
