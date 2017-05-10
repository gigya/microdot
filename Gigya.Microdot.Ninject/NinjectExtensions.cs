using System;
using System.Collections.Concurrent;

using Ninject;

namespace Gigya.Microdot.Ninject
{
    public static class NinjectExtensions
    {
        /// <summary>
        /// Binds <see cref="TService"/> to <see cref="TImplementation"/> and configures Ninject factories in the form
        /// of <see cref="Func{TKey, TService}"/> and <see cref="Func{TKey, TImplementation}"/> to return the same
        /// instance every time the factory is called with the same parameter of type <see cref="TKey"/>.
        /// </summary>
        /// <typeparam name="TKey">The type of the parameter passed to the factory.</typeparam>
        /// <typeparam name="TService">The type of the service (or interface) to bind.</typeparam>
        /// <typeparam name="TImplementation">The type of the implementation of the service to bind to.</typeparam>
        /// <param name="kernel">The Ninject kernel on which to set up the binding.</param>
        public static void BindPerKey<TKey, TService, TImplementation>(this IKernel kernel)
            where TImplementation : TService
        {
            var dict = new ConcurrentDictionary<TKey, TService>();

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
            var dict = new ConcurrentDictionary<TKey, TService>();

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
            var dict = new ConcurrentDictionary<Tuple<TKey1, TKey2>, TService>();

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
    }
}