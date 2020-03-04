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

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <remarks>
    /// Not using Ninject scope feature to prevent memory leak.
    /// All dependency references should only be rooted through ServiceScope, so they would
    /// be collected with the scope itself.
    /// It prevents memory leak that was caused by lack of user scope cleanup
    /// (not calling dispose at the end of the scope) and keeping dependencies rooted in Ninject cache.
    /// </remarks>

    /// decorator on IServiceProvider that manage the life time of Service on Scope


    internal class CacheItem : IDisposable
    {

        private Dictionary<Type, object> _scopeServices;
        private List<IDisposable> _disposables;
        private readonly object _locker = new object();
        private readonly IRequestScopedType _requestScoped;
        public CacheItem(IRequestScopedType requestScoped)
        {
            this._requestScoped = requestScoped;
        }
        public void Dispose()
        {
            if (_disposables != null)
            {
                foreach (var disposable in _disposables)
                {
                    disposable.Dispose();
                }
            }
        }


        private void EnsureScopeMapsInitialized()
        {
            //Most of the time no one is creating service in scope
            if (_scopeServices == null)
            {
                _scopeServices = new Dictionary<Type, object>();
                _disposables = new List<IDisposable>();
            }

        }
        public bool TryGet(Type key, out object instance)
        {
            if (_requestScoped.Contains(key) == false)
            {
                instance = null;
                return false;
            }
            lock (_locker)
            {
                EnsureScopeMapsInitialized();
                return _scopeServices.TryGetValue(key, out instance);
            }
        }

        public void Add(Type key, object instance)
        {
            if (_requestScoped.Contains(key) == false)
            {
                // Should no manger this life time
                return;
            }

            lock (_locker)
            {
                EnsureScopeMapsInitialized();
                _scopeServices.Add(key, instance);
                if (instance is IDisposable disposable)
                {
                    _disposables.Add(disposable);
                }
            }
        }

    }

}
