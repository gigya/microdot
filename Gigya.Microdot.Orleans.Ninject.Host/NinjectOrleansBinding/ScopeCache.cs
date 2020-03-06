using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Hold direcet refrance to all scope depency and mange there life time.
    /// </summary>
    internal class ScopeCache : IDisposable
    {
        private ImmutableDictionary<Type, object> _scopeServices;
        private List<IDisposable> _disposables;
        private readonly object _locker = new object();

        public ScopeCache()
        {
            _scopeServices = ImmutableDictionary.CreateBuilder<Type, object>().ToImmutable();
            _disposables = new List<IDisposable>();
        }

        public void Dispose()
        {
            if (_disposables != null)
            {
                lock (_locker)
                {

                    foreach (var disposable in _disposables)
                    {
                        disposable.Dispose();
                    }
                    _disposables = null;
                    _scopeServices = null;
                }
            }
        }



        public object GetORCreate(Type key, Func<object> instancefactory)
        {
            var scopeService = _scopeServices;
            if (scopeService == null)
            {
                throw new ObjectDisposedException("cacheItem");
            }

            // the assmption that you create few object on scope but resovle the many time
            // scopeService mast be safe therad object !!(Out side the lock)
            if (scopeService.TryGetValue(key, out var result))
            {
                return result;
            }

            lock (_locker)
            {
                if (_scopeServices == null)
                {
                    throw new ObjectDisposedException("cacheItem");
                }

                if (_scopeServices.TryGetValue(key, out result))
                {
                    return result;
                }

                var instance = instancefactory();
                _scopeServices = _scopeServices.Add(key, instance);
                if (instance is IDisposable disposable)
                {
                    _disposables?.Add(disposable);
                }
                return instance;
            }
        }
    }
}
