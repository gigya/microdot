using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

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
    internal class MicrodotServiceScope : IServiceScope, IServiceProvider
    {
        private readonly IRequestScopedType _scopedType;
        private readonly object _locker = new object();

        // ReSharper disable once CollectionNeverQueried.Local
        private Dictionary<Type, object> _scopeServices;
        private List<IDisposable> _disposables;
        
        private readonly IServiceProvider _component;
        public MicrodotServiceScope(IServiceProvider serviceProvider, IRequestScopedType scopedType)
        {
            _scopedType = scopedType;
            _component = serviceProvider;
        }

        public IServiceProvider ServiceProvider => this;

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

        public object GetService(Type serviceType)
        {
            if (_scopedType.Contains(serviceType))
            {
                lock (_locker)
                {
                    EnsureScopeMapsInitialized();
                    if (_scopeServices.TryGetValue(serviceType, out var service))
                    {
                        return service;
                    }

                    var result = _component.GetService(serviceType);
                    _scopeServices.Add(serviceType, result);
                    if (result is IDisposable disposable)
                    {
                        _disposables.Add(disposable);
                    }
                    return result;
                }
            }

            else
            {
                return _component.GetService(serviceType);
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
    }
}