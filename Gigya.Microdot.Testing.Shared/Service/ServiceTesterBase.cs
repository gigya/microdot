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

#endregion Copyright

using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Parameters;
using Ninject.Syntax;
using System;

namespace Gigya.Microdot.Testing.Shared.Service
{
    public abstract class ServiceTesterBase : IDisposable
    {
        protected IResolutionRoot ResolutionRoot { get; set; }

        public int BasePort { get; protected set; }

        /// <summary>
        /// GetObject a ServiceProxy with caching  that is configured to call the service under test. Both the port and the hostname of
        /// the provided ServiceProxy is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TServiceInterface"></typeparam>
        /// <returns>An ServiceProxy with caching.</returns>
        public virtual TServiceInterface GetServiceProxyWithCaching<TServiceInterface>(TimeSpan? timeout = null)
        {
            var factory = ResolutionRoot
                .Get<Func<string, Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>, IServiceProxyProvider>>();
            var provider = new ServiceProxyProvider<TServiceInterface>(serviceName => factory(serviceName,
                (serName, checker) => new LocalhostServiceDiscovery(ResolutionRoot.Get<CurrentApplicationInfo>())));

            provider.DefaultPort = BasePort;
            if (timeout != null)
                provider.InnerProvider.SetHttpTimeout(timeout.Value);
            if (ResolutionRoot.Get<IMetadataProvider>().HasCachedMethods(typeof(TServiceInterface)))
            {
                var cachingProxy = ResolutionRoot.Get<CachingProxyProvider<TServiceInterface>>(
                    new ConstructorArgument("dataSource", provider.Client),
                    new ConstructorArgument("serviceName", (string)null));

                return cachingProxy.Proxy;
            }
            return provider.Client;
        }

        /// <summary>
        /// GetObject a ServiceProxy that is configured to call the service under test. Both the port and the hostname of
        /// the provided ServiceProxy is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TServiceInterface"></typeparam>
        /// <returns>An ServiceProxy instance/>.</returns>
        public virtual TServiceInterface GetServiceProxy<TServiceInterface>(TimeSpan? timeout = null)
        {
            var factory = ResolutionRoot.Get<Func<string, Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>, IServiceProxyProvider>>();

            var provider = new ServiceProxyProvider<TServiceInterface>(serviceName => factory(serviceName,
                (serName, checker) => new LocalhostServiceDiscovery(ResolutionRoot.Get<CurrentApplicationInfo>())));
            provider.DefaultPort = BasePort;
            if (timeout != null)
                provider.InnerProvider.SetHttpTimeout(timeout.Value);

            return provider.Client;
        }

        /// <summary>
        /// GetObject a ServiceProxy that is configured to call the service under test. Both the port and the hostname of
        /// the provided ServiceProxy is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="serviceName">Name of service </param>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <returns>An ServiceProxy instance"/>.</returns>
        public virtual IServiceProxyProvider GetServiceProxyProvider(string serviceName, TimeSpan? timeout = null)
        {
            var factory = ResolutionRoot.Get<Func<string, Func<string, ReachabilityCheck, IMultiEnvironmentServiceDiscovery>, ServiceProxyProvider>>();

            var provider = factory(serviceName, (srName, r) => new LocalhostServiceDiscovery(ResolutionRoot.Get<CurrentApplicationInfo>()));
            provider.DefaultPort = BasePort;
            if (timeout != null)
                provider.SetHttpTimeout(timeout.Value);

            return provider;
        }

        public abstract void Dispose();
    }
}