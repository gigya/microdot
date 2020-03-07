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
    /// Hold direct reference to all ServiceProvider Scope decencies and mange there life time.
    /// /// </summary>
    internal class ScopeCache : IDisposable
    {
        private ImmutableDictionary<Type, object> _scopeServices;
        private List<IDisposable> _disposables;
        private readonly object _locker = new object();
        private int _isDispose = 0;
        public ScopeCache()
        {
            _scopeServices = ImmutableDictionary.CreateBuilder<Type, object>().ToImmutable();
            _disposables = new List<IDisposable>();
        }

        public void Dispose()
        { 
            // Should not lock to avoid deadlock
            // UseCase: When worker thread is during resolve during Kernel dispose
            if (Interlocked.CompareExchange(ref _isDispose, 1, 0) == 0)
            {

                foreach (var disposable in _disposables)
                {
                    disposable.Dispose();
                }
                _disposables = null;
                _scopeServices = null;
            }

        }
               
        public object GetOrCreate(Type key, Func<object> instancefactory)
        {
            var scopeService = _scopeServices;
            if (scopeService == null)
            {
                throw new ObjectDisposedException("cacheItem");
            }

            // The assumption that you create few object on scope but resolve the many time
            // scopeService mast be safe thread object !!(Out side the lock)
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

                // Optimistic lock regarding the dispose
                // Don't need to use interlock inside lock
                if (_isDispose == 1)
                {
                    // Skipping dispose of the race condition instance in case it IDisposable   
                    // In order to keep the code simple and not doing it inside the lock
                    _scopeServices = null;
                    _disposables = null;
                    throw new ObjectDisposedException("cacheItem");
                }

                return instance;
            }
        }
    }
}
