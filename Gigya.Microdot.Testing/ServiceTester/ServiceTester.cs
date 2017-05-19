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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Parameters;
using Ninject.Syntax;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;

namespace Gigya.Microdot.Testing.ServiceTester
{
    public class ServiceTester<TServiceHost> : IDisposable where TServiceHost : MicrodotOrleansServiceHost, new()
    {
        private ILog Log { get; set; }
        private IResolutionRoot ResolutionRoot { get; set; }
        public AppDomain ServiceAppDomain { get; private set; }

        public static TServiceHost Host { get; private set; }
        private static Task StopTask;

        private int BasePort { get; set; }
        private HttpListener LogListener { get; set; }


        public ServiceTester(int? basePortOverride, bool isSecondary, ILog log, IResolutionRoot resolutionRoot)
        {
            Log = log;
            ResolutionRoot = resolutionRoot;
            // ReSharper disable VirtualMemberCallInContructor
            InitializeInfrastructure();

            var serviceArguments = GetServiceArguments(basePortOverride, isSecondary);
            
            BasePort = serviceArguments.BasePortOverride.Value;
            ServiceAppDomain = Common.CreateDomain(typeof(TServiceHost).Name + BasePort);
            StartLogListener(BasePort, ServiceAppDomain);

            ServiceAppDomain.RunOnContext(serviceArguments, args =>
            {
                Host?.Stop();
                Host?.Dispose();
                Host = new TServiceHost();
                StopTask = Host.RunAsync(args);
            });
        }

        
        /// <summary>
        /// Get a ServiceProxy that is configured to call the service under test. Both the port and the hostname of
        /// the provided ServiceProxy is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TServiceInterface"></typeparam>
        /// <returns>An ServiceProxy instance of <see cref="TServiceInterface"/>.</returns>
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
        /// Get a ServiceProxy that is configured to call the service under test. Both the port and the hostname of
        /// the provided ServiceProxy is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        
        /// <returns>An ServiceProxy instance of <see cref="TServiceInterface"/>.</returns>
        public virtual ServiceProxyProvider GetServiceProxyProvider(string serviceName, TimeSpan? timeout = null)
        {
            var factory = ResolutionRoot.Get<Func<string, Func<string, ReachabilityChecker, IServiceDiscovery>, ServiceProxyProvider>>();

            var provider = factory(serviceName, (srName, r) => new LocalhostServiceDiscovery());
            provider.DefaultPort = BasePort;      
			if (timeout != null)
                provider.SetHttpTimeout(timeout.Value);
               
            return provider;
        }


        /// <summary>
        /// Get a GrainClient that is configured to call the service under test. Both the port and the hostname of
        /// the provided GrainClient is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="primaryKey">The primary key of the grain that you want to call.</param>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TGrainInterface"></typeparam>
        /// <returns>An GrainClient instance of <see cref="TGrainInterface"/>.</returns>
        public virtual TGrainInterface GetGrainClient<TGrainInterface>(long primaryKey, TimeSpan? timeout = null) where TGrainInterface : IGrainWithIntegerKey
        {
            InitGrainClient(timeout);
            return GrainClient.GrainFactory.GetGrain<TGrainInterface>(primaryKey);
        }


        /// <summary>
        /// Get a GrainClient that is configured to call the service under test. Both the port and the hostname of
        /// the provided GrainClient is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="primaryKey">The primary key of the grain that you want to call.</param>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TGrainInterface"></typeparam>
        /// <returns>An GrainClient instance of <see cref="TGrainInterface"/>.</returns>
        public virtual TGrainInterface GetGrainClient<TGrainInterface>(string primaryKey, TimeSpan? timeout = null) where TGrainInterface : IGrainWithStringKey
        {
            InitGrainClient(timeout);
            return GrainClient.GrainFactory.GetGrain<TGrainInterface>(primaryKey);
        }


        /// <summary>
        /// Get a GrainClient that is configured to call the service under test. Both the port and the hostname of
        /// the provided GrainClient is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="primaryKey">The primary key of the grain that you want to call.</param>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TGrainInterface"></typeparam>
        /// <returns>An GrainClient instance of <see cref="TGrainInterface"/>.</returns>
        public virtual TGrainInterface GetGrainClient<TGrainInterface>(Guid primaryKey, TimeSpan? timeout = null) where TGrainInterface : IGrainWithGuidKey
        {
            InitGrainClient(timeout);
            return GrainClient.GrainFactory.GetGrain<TGrainInterface>(primaryKey);
        }

        /// <summary>
        /// Get a GrainClient that is configured to call the service under test. Both the port and the hostname of
        /// the provided GrainClient is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="primaryKey">The primary key of the grain that you want to call.</param>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TGrainInterface"></typeparam>
        /// <returns>An GrainClient instance of <see cref="TGrainInterface"/>.</returns>
        public virtual TGrainInterface GetGrainClient<TGrainInterface>(int primaryKey, string keyExtension, TimeSpan? timeout = null) where TGrainInterface : IGrainWithIntegerCompoundKey
        {
            InitGrainClient(timeout);
            return GrainClient.GrainFactory.GetGrain<TGrainInterface>(primaryKey, keyExtension, null);
        }

        /// <summary>
        /// Get a GrainClient that is configured to call the service under test. Both the port and the hostname of
        /// the provided GrainClient is changed to match those of the service which was started by the ServiceTester.
        /// </summary>
        /// <param name="primaryKey">The primary key of the grain that you want to call.</param>
        /// <param name="timeout">Optional. The timeout for ServiceProxy calls.</param>
        /// <typeparam name="TGrainInterface"></typeparam>
        /// <returns>An GrainClient instance of <see cref="TGrainInterface"/>.</returns>
        public virtual TGrainInterface GetGrainClient<TGrainInterface>(Guid primaryKey, string keyExtension, TimeSpan? timeout = null) where TGrainInterface : IGrainWithGuidCompoundKey
        {
            InitGrainClient(timeout);
            return GrainClient.GrainFactory.GetGrain<TGrainInterface>(primaryKey, keyExtension, null);
        }
        

        /// <summary>
        /// Immediately unloads the service's AppDomain without graceful shutdown. You should still call
        /// <see cref="Dispose"/> afterwards.
        /// </summary>
        public virtual void Kill()
        {
            try
            {
                AppDomain.Unload(ServiceAppDomain);
                ServiceAppDomain = null;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to unload AppDomain of service. Please make sure you've binded a fake IMetricsInitializer.", ex);
            }
        }

        public virtual void Dispose()
        {
            if (GrainClient.IsInitialized)
                GrainClient.Uninitialize();

            if (ServiceAppDomain != null)
            {
                ServiceAppDomain.RunOnContext(() =>
                {
                    Host.Stop();

                    Host.Dispose();

                    var completed = StopTask.Wait(60000);

                    if (!completed)
                        throw new TimeoutException("ServiceTester: The service failed to shutdown within the 60 second limit.");
                });

                Kill();
            }

            LogListener.Close();
        }


        protected virtual void InitializeInfrastructure()
        {
            LogManager.Initialize(new TestTraceConfiguration());
            LogManager.LogConsumers.Add(new OrleansLogConsumer(Log));
        }


        protected virtual ServiceArguments GetServiceArguments(int? basePortOverride, bool isSecondary)
        {
            if (isSecondary && basePortOverride == null)
                throw new ArgumentException("You must specify a basePortOverride when running a secondary silo.");

            var siloClusterMode = isSecondary ? SiloClusterMode.SecondaryNode : SiloClusterMode.PrimaryNode;
            ServiceArguments arguments = new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive, basePortOverride: basePortOverride, siloClusterMode: siloClusterMode);

            if (basePortOverride != null)
                return arguments;

            var serviceArguments = new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive, siloClusterMode: siloClusterMode);
            var commonConfig = new BaseCommonConfig(serviceArguments);
            var mapper = new OrleansServiceInterfaceMapper(new AssemblyProvider(new ApplicationDirectoryProvider(commonConfig), commonConfig, Log));
            var basePort = mapper.ServiceInterfaceTypes.First().GetCustomAttribute<HttpServiceAttribute>().BasePort;

            return new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive, basePortOverride: basePort);
        }


        protected virtual void StartLogListener(int basePort, AppDomain appDomain)
        {
            var httpLogListenPort = basePort - 1;
            appDomain.SetData("HttpLogListenPort", httpLogListenPort);
            LogListener = new HttpListener { Prefixes = { $"http://localhost:{httpLogListenPort}/" } };
            LogListener.Start();
            LogListenerLoop();
        }


        private async void LogListenerLoop()
        {
            while (true)
            {
                try
                {
                    var context = await LogListener.GetContextAsync().ConfigureAwait(false);

                    using (context.Response)
                    {
                        if (context.Request.RawUrl != "/log")
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            continue;
                        }

                        string log = await new StreamReader(context.Request.InputStream).ReadToEndAsync().ConfigureAwait(false);
                        Console.WriteLine(log);
                        context.Response.StatusCode = 200;
                    }
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    break; // "The I/O operation has been aborted because of either a thread exit or an application request" (no idea what it means)
                }
                catch (ObjectDisposedException)
                {
                    break; // Listener has been stopped, GetContextAsync() is aborted.
                }
                catch (Exception ex)
                {
                    Console.WriteLine("LogListener: An error has occured during HttpListener.GetContextAsync(): " + ex);
                }
            }
        }

        protected virtual void InitGrainClient(TimeSpan? timeout)
        {
            if (GrainClient.IsInitialized == false)
            {
                var clientConfiguration = new ClientConfiguration
                {
                    Gateways =
                    {
                        new IPEndPoint(IPAddress.Loopback, BasePort + (int)PortOffsets.SiloGateway)
                    },
                    StatisticsWriteLogStatisticsToTable = false
                };

                if (timeout != null)
                    clientConfiguration.ResponseTimeout = timeout.Value;

                GrainClient.Initialize(clientConfiguration);
            }
        }
    }

    public class TestTraceConfiguration : ITraceConfiguration
    {
        public Severity DefaultTraceLevel { get; set; } = Severity.Warning;
        public string TraceFileName { get; set; } = null;
        public string TraceFilePattern { get; set; } = null;
        public IList<Tuple<string, Severity>> TraceLevelOverrides { get; } = new List<Tuple<string, Severity>>();
        public bool TraceToConsole { get; set; } = false;
        public bool WriteMessagingTraces { get; set; }
        public int LargeMessageWarningThreshold { get; set; }
        public bool PropagateActivityId { get; set; }
        public int BulkMessageLimit { get; set; }
    }



    public static class ServiceTesterExtensions
    {
        public static ServiceTester<TServiceHost> GetServiceTester<TServiceHost>(this IResolutionRoot kernel, int? basePortOverride = null, bool isSecondary = false)
            where TServiceHost : MicrodotOrleansServiceHost, new()
        {
            ServiceTester<TServiceHost> tester = kernel.Get<ServiceTester<TServiceHost>>(
                new ConstructorArgument(nameof(basePortOverride), basePortOverride),
                new ConstructorArgument(nameof(isSecondary), isSecondary));
            
            return tester;
        }
    }
}
