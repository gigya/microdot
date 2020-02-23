using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Gigya.Microdot.Orleans.Ninject.Host.IOC
{
    /// <summary>
    /// Not using ninject scope feature do prevent memory leak
    /// BY designed ServiceScope should be the only root that hold all scope service.
    /// it prevent memory leak by letting scope be collected even when not dispose if not reference any more
    /// </summary>
    internal class MicroDotServiceScope : IServiceScope, IServiceProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RequestBindingType _bindingType;
        private readonly object _locker = new object();

        // ReSharper disable once CollectionNeverQueried.Local
        private Dictionary<Type, object> _scopeServices;
        private List<IDisposable> _disposables;
        public MicroDotServiceScope(IServiceProvider serviceProvider, RequestBindingType bindingType)
        {
            _serviceProvider = serviceProvider;
            ServiceProvider = serviceProvider;
            _bindingType = bindingType;
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

        public IServiceProvider ServiceProvider { get; }
        public object GetService(Type serviceType)
        {
            if (_bindingType.IsRequest(serviceType))
            {
                lock (_locker)
                {
                    //Most of the time no one is creating service in scope
                    if (_scopeServices == null)
                    {
                        _scopeServices = new Dictionary<Type, object>();
                        _disposables = new List<IDisposable>();
                    }
                    else if (_scopeServices.TryGetValue(serviceType, out var service))
                    {
                        return service;
                    }

                    
                    var result = _serviceProvider.GetService(serviceType);
                    _scopeServices.Add(serviceType, result);
                    if (serviceType is IDisposable disposable)
                    {
                        _disposables.Add(disposable);
                    }
                    return result;
                }
            }

            else
            {
                return _serviceProvider.GetService(serviceType);
            }

        }
    }
}