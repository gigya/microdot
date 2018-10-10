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
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Parameters;
using Ninject.Syntax;

namespace Gigya.Microdot.Testing.Shared.Service
{
    public abstract class ServiceTesterBase : IDisposable
    {
        protected ILog Log { get; }

        protected IResolutionRoot ResolutionRoot { get; set; }

        protected int BasePort { get; set; }

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
                .Get<Func<string, Func<string, ReachabilityChecker, IServiceDiscovery>, IServiceProxyProvider>>();
            var provider = new ServiceProxyProvider<TServiceInterface>(serviceName => factory(serviceName, (serName, checker) => new LocalhostServiceDiscovery()));

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
            var factory = ResolutionRoot.Get<Func<string, Func<string, ReachabilityChecker, IServiceDiscovery>, IServiceProxyProvider>>();

            var provider = new ServiceProxyProvider<TServiceInterface>(serviceName => factory(serviceName, (serName, checker) => new LocalhostServiceDiscovery()));
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
        public virtual ServiceProxyProvider GetServiceProxyProvider(string serviceName, TimeSpan? timeout = null)
        {
            var factory = ResolutionRoot.Get<Func<string, Func<string, ReachabilityChecker, IServiceDiscovery>, ServiceProxyProvider>>();

            var provider = factory(serviceName, (srName, r) => new LocalhostServiceDiscovery());
            provider.DefaultPort = BasePort;
            if (timeout != null)
                provider.SetHttpTimeout(timeout.Value);

            return provider;
        }

        protected virtual ServiceArguments GetServiceArguments(int? basePortOverride, bool isSecondary, int? shutdownWaitTime, ServiceStartupMode startupMode = ServiceStartupMode.CommandLineNonInteractive)
        {
            if (isSecondary && basePortOverride == null)
                throw new ArgumentException("You must specify a basePortOverride when running a secondary silo.");

            var siloClusterMode = isSecondary ? SiloClusterMode.SecondaryNode : SiloClusterMode.PrimaryNode;
            ServiceArguments arguments = new ServiceArguments(startupMode, basePortOverride: basePortOverride, siloClusterMode: siloClusterMode, shutdownWaitTimeSec: shutdownWaitTime);

            if (basePortOverride != null)
                return arguments;

            var serviceArguments = new ServiceArguments(startupMode, siloClusterMode: siloClusterMode, shutdownWaitTimeSec: shutdownWaitTime);
            var commonConfig = new BaseCommonConfig(serviceArguments);
            var mapper = new OrleansServiceInterfaceMapper(new AssemblyProvider(new ApplicationDirectoryProvider(commonConfig), commonConfig, Log));
            var basePort = mapper.ServiceInterfaceTypes.First().GetCustomAttribute<HttpServiceAttribute>().BasePort;

            return new ServiceArguments(startupMode, basePortOverride: basePort, shutdownWaitTimeSec: shutdownWaitTime);
        }

        public abstract void Dispose();
    }
}