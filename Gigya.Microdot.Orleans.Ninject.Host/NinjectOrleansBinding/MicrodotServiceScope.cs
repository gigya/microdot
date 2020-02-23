using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Not using ninject scope feature do prevent memory leak
    /// BY designed ServiceScope should be the only root that hold all scope service.
    /// it prevent memory leak by letting scope be collected even when not dispose if not reference any more
    /// </summary>
    internal class MicrodotServiceScope : IServiceScope, IServiceProvider
    {
        private readonly RequestScopedType _scopedType;
        private readonly object _locker = new object();

        // ReSharper disable once CollectionNeverQueried.Local
        private Dictionary<Type, object> _scopeServices;
        private List<IDisposable> _disposables;
        public MicrodotServiceScope(IServiceProvider serviceProvider, RequestScopedType scopedType)
        {
            ServiceProvider = serviceProvider;
            _scopedType = scopedType;
        }
        public IServiceProvider ServiceProvider { get; }

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
                    
                    var result = ServiceProvider.GetService(serviceType);
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
                return ServiceProvider.GetService(serviceType);
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