#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Concurrent;
using System.Linq;
using Ninject;
using Ninject.Planning.Bindings;

#pragma warning disable 1574

namespace Gigya.Microdot.Ninject
{
    public class DisposableCollection<TKey, TService> : IDisposable
    {
        private readonly IKernel _kernel;
        readonly ConcurrentDictionary<TKey, TService> _dictionary = new ConcurrentDictionary<TKey, TService>();

        public DisposableCollection(IKernel kernel)
        {
            _kernel = kernel;
        }

        public TService GetOrAdd(TKey key, Func<TKey, TService> factory)
        {
            return _dictionary.GetOrAdd(key, factory);
        }

        public TService GetOrAdd(TKey key, TService service)
        {
            return _dictionary.GetOrAdd(key, service);
        }

        public void Dispose()
        {
            IDisposable[] disposables = null;
            //Lock to the kernel this is the implication of ninject singleton scope
           
                disposables = _dictionary.Values.
                    Select(x => x as IDisposable).
                    Where(x => x != null).ToArray();
          

            foreach (var disposable in disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }


    public static class NinjectExtensions
{
    /// <summary>
    /// Binds <see cref="TService"/> to <see cref="TImplementation"/> and configures Ninject factories in the form
    /// of <see cref="Func{T,TResult}"/> and <see cref="Func{TKey, TImplementation}"/> to return the same
    /// instance every time the factory is called with the same parameter of type <see cref="TKey"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the parameter passed to the factory.</typeparam>
    /// <typeparam name="TService">The type of the service (or interface) to bind.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
    /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
    public static void BindPerKey<TKey, TService, TImplementation>(this IKernel kernel)
        where TImplementation : TService
    {
        var dict = kernel.Get<DisposableCollection<TKey, TService>>();

        kernel.Rebind<TService>().To<TImplementation>();

        var factory = kernel.Get<Func<TKey, TService>>();

        kernel.Rebind<Func<TKey, TService>>()
            .ToMethod(c => key => dict.GetOrAdd(key, _ => factory(key)))
            .InSingletonScope();

        if (typeof(TImplementation) != typeof(TService))
        {
            kernel.Rebind<Func<TKey, TImplementation>>()
                .ToMethod(c => key => (TImplementation)dict.GetOrAdd(key, _ => factory(key)))
                .InSingletonScope();
        }

    }

    /// <summary>
    /// Binds <see cref="TService"/> to <see cref="TImplementation"/> and configures Ninject factories in the form
    /// of <see cref="Func{TKey, TParam, TService}"/> and <see cref="Func{TKey, TParam, TImplementation}"/> to 
    /// return the same instance every time the factory is called with the same parameter of type <see cref="TKey"/>.
    /// The additional parameter of type <see cref="TParam"/> is not used to discriminate the returned instances,
    /// and is only passed along to the factory.
    /// </summary>
    /// <typeparam name="TKey">The type of the parameter passed to the factory.</typeparam>
    /// <typeparam name="TParam">The type of an additional parameter passed to the factory. It is not to 
    /// discriminate which instance of <see cref="TService"/> is returned.</typeparam>
    /// <typeparam name="TService">The type of the service (or interface) to bind.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
    /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
    public static void BindPerKey<TKey, TParam, TService, TImplementation>(this IKernel kernel)
        where TImplementation : TService
    {
        var dict = kernel.Get<DisposableCollection<TKey, TService>>();

        kernel.Rebind<TService>().To<TImplementation>();

        var factory = kernel.Get<Func<TKey, TParam, TService>>();

        kernel.Rebind<Func<TKey, TParam, TService>>()
              .ToMethod(c => (key, param) => dict.GetOrAdd(key, _ => factory(key, param)))
              .InSingletonScope();

        if (typeof(TImplementation) != typeof(TService))
        {
            kernel.Rebind<Func<TKey, TParam, TImplementation>>()
                  .ToMethod(c => (key, param) => (TImplementation)dict.GetOrAdd(key, _ => factory(key, param)))
                  .InSingletonScope();
        }
    }


    /// <summary>
    /// Binds <see cref="TService"/> to <see cref="TImplementation"/> and configures Ninject factories in the form
    /// of <see cref="Func{TKey1, TKey2, TService}"/> and <see cref="Func{TKey1, TKey2, TImplementation}"/> to 
    /// return the same instance every time the factory is called with the same pair of parameters of type 
    /// <see cref="TKey1"/> and <see cref="TKey2"/>.
    /// </summary>
    /// <typeparam name="TKey1">The type of the first parameter passed to the factory. Both this parameter and
    /// <see cref="TKey2"/> are used to discriminate which instance of <see cref="TService"/> is returned.</typeparam>
    /// <typeparam name="TKey2">The type of the second parameter passed to the factory. Both this parameter and
    /// <see cref="TKey1"/> are used to discriminate which instance of <see cref="TService"/> is returned.</typeparam>
    /// <typeparam name="TService">The type of the service (or interface) to bind.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
    /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
    public static void BindPerMultiKey<TKey1, TKey2, TService, TImplementation>(this IKernel kernel)
        where TImplementation : TService
    {
        var dict = kernel.Get<DisposableCollection<Tuple<TKey1, TKey2>, TService>>();

        kernel.Rebind<TService>().To<TImplementation>();

        var factory = kernel.Get<Func<TKey1, TKey2, TService>>();

        kernel.Rebind<Func<TKey1, TKey2, TService>>()
              .ToMethod(c => (k1, k2) => dict.GetOrAdd(Tuple.Create(k1, k2), _ => factory(k1, k2)))
              .InSingletonScope();

        if (typeof(TImplementation) != typeof(TService))
        {
            kernel.Rebind<Func<TKey1, TKey2, TImplementation>>()
                  .ToMethod(c => (k1, k2) => (TImplementation)dict.GetOrAdd(Tuple.Create(k1, k2), _ => factory(k1, k2)))
                  .InSingletonScope();
        }
    }


    /// <summary>
    /// Configures Ninject factories in the form of <see cref="Func{TKey, TImplementation}"/> to return the same
    /// instance every time the factory is called with the same parameter of type <see cref="TKey"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the parameter passed to the factory.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
    /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
    public static void BindPerKey<TKey, TImplementation>(this IKernel kernel)
    {
        kernel.BindPerKey<TKey, TImplementation, TImplementation>();
    }

    /// <summary>
    /// Binds <see cref="TService"/> to <see cref="TImplementation"/> and configures Ninject factories in the form
    /// of <see cref="Func{TString, TService}"/> and <see cref="Func{TString, TImplementation}"/> to return the same
    /// instance every time the factory is called with the same <see cref="string"/>.
    /// </summary>
    /// <typeparam name="TService">The type of the service (or interface) to bind.</typeparam>
    /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
    /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
    public static void BindPerString<TService, TImplementation>(this IKernel kernel)
        where TImplementation : TService
    {
        kernel.BindPerKey<string, TService, TImplementation>();
    }

    /// <summary>
    /// Configures Ninject factories in the form of <see cref="Func{TString, TImplementation}"/> to return the same
    /// instance every time the factory is called with the same <see cref="string"/>.
    /// </summary>
    /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
    /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
    public static void BindPerString<TImplementation>(this IKernel kernel)
    {
        kernel.BindPerString<TImplementation, TImplementation>();
    }

    public static bool IsBinded(this IKernel kernel, Type serviceType)
    {
        IBinding binding = kernel.GetBindings(serviceType).FirstOrDefault();

        return binding != null && binding.Target != BindingTarget.Provider;
    }
}

  
}