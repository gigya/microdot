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

using Microsoft.Extensions.DependencyInjection;
using Ninject;
using Ninject.Syntax;
using System;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    ///  Service locater abstraction .
    ///  Every service provider has it's own scope.
    /// </summary>

    internal class MicrodotServiceProviderWithScope : IServiceProvider, IServiceScope, IGlobalServiceProvider
    {
        private readonly IResolutionRoot _resolver;
        internal readonly MicrodotNinjectScopeParameter _microdotNinectScopParameter;
        private readonly ScopeCache _cacheItem;
        private readonly static object _obj = new object();


        public MicrodotServiceProviderWithScope(IResolutionRoot resolver)
        {
            _cacheItem = new ScopeCache();
            _microdotNinectScopParameter = new MicrodotNinjectScopeParameter(_cacheItem, this);
            _resolver = resolver;
        }

        public IServiceProvider ServiceProvider => this;

        public void Dispose()
        {
            _cacheItem.Dispose();
        }

        public object GetService(Type type)
        {
            var guid = Guid.NewGuid().ToString("N");
            Console.WriteLine($"*** {nameof(type)} - {guid}");
            object res;

            lock (_obj)
            {
                Console.WriteLine($"*** Inside1 lock on MicrodotServiceProviderWithScope - {nameof(type)} - {guid}");
                res = _resolver.Get(type, _microdotNinectScopParameter);
                Console.WriteLine($"*** Inside2 lock on MicrodotServiceProviderWithScope - {nameof(type)} - {guid}");
            }
            Console.WriteLine($"*** After lock on MicrodotServiceProviderWithScope - {guid}");
            return res;
        }

    }
}
