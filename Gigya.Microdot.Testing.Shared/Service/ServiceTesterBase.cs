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
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.UnitTests.Caching.Host;

namespace Gigya.Microdot.Testing.Shared.Service
{
    public abstract class ServiceTesterBase : IDisposable
    {
        private readonly IKernel _kernel;
        protected IResolutionRoot ResolutionRoot;

        public int BasePort { get; protected set; }



        public ServiceTesterBase(Action<IBindingRoot> additionalBinding = null)
        {
            _kernel = new MicrodotInitializer("", new ConsoleLogLoggersModules(),
                (kernel =>
                {
                    additionalBinding?.Invoke(kernel);

                })).Kernel;
            ResolutionRoot = _kernel;
        }
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
                (serName, checker) => new LocalhostServiceDiscovery()));

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
                (serName, checker) => new LocalhostServiceDiscovery()));
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

            var provider = factory(serviceName, (srName, r) => new LocalhostServiceDiscovery());
            provider.DefaultPort = BasePort;
            if (timeout != null)
                provider.SetHttpTimeout(timeout.Value);

            return provider;
        }

        public virtual void Dispose()
        {
            _kernel.Dispose();
        }
        private static List<Semaphore> portMaintainer = new List<Semaphore>();

        public static int GetPort()
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            List<int> occupiedPortsData= new List<int>();
            occupiedPortsData.AddRange( ipGlobalProperties.GetActiveTcpConnections().Select(x=>x.LocalEndPoint.Port));
            occupiedPortsData.AddRange(ipGlobalProperties.GetActiveTcpListeners().Select(x => x.Port));
            occupiedPortsData.AddRange(ipGlobalProperties.GetActiveUdpListeners().Select(x => x.Port));


            var occupiedPorts = occupiedPortsData.Distinct().ToHashSet();

            for (int retry = 0; retry < 10000; retry++)
            {
                var randomPort = new Random( ).Next(50000, 60000);
                bool freeRangePort = true;
                int range = Enum.GetValues(typeof(PortOffsets)).Cast<int>().Max();

                for (int port = randomPort; port <= randomPort + range; port++)
                {
                    freeRangePort = freeRangePort && (occupiedPorts.Contains(port) == false);
                    if (!freeRangePort)
                        break;
                }
                bool someOneElseWantThisPort = false;
                if (freeRangePort)
                {

                    // We need to avoid race condition between different App Domains and processes running in 
                    // parallel and allocating the same port, especially the tests running in parallel.
                    // The semaphore is machine / OS wide, so the hope it is good enough.

                    for (int port = randomPort; port <= randomPort + range; port++)
                    {
                        var name = $"ServiceTester-{port}";
                        if (Semaphore.TryOpenExisting(name, out var _))
                        {
                            someOneElseWantThisPort = true;
                        }
                        else
                        {
                            try
                            {
                                portMaintainer.Add(new Semaphore(1, 1, name));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                someOneElseWantThisPort = true;
                            }
                        }
                    }

                    if (someOneElseWantThisPort == false)
                    {
                        Console.WriteLine($"Service Tester found a free port: {randomPort}");
                        return randomPort;
                    }
                }
            }

            throw new Exception("can't find free port ");
        }
    }
}