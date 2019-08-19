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

using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Shared.Service;
using Orleans;
using Orleans.Configuration;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Gigya.Microdot.Testing.Service
{
    public class ServiceTester<TServiceHost> : ServiceTesterBase where TServiceHost : MicrodotOrleansServiceHost, new()
    {
        private readonly Type _customSerializer;
        public TServiceHost Host { get; private set; }
        public Task SiloStopped { get; private set; }

        private IClusterClient _clusterClient;
        
        private readonly object _locker = new object();

        public ServiceArguments ServiceArguments{ get; private set; }

        public ServiceTester(ServiceArguments serviceArguments = null, Type customSerializer = null)
        {
            _customSerializer = customSerializer;

            ServiceArguments = serviceArguments ?? new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive, 
                            ConsoleOutputMode.Disabled, 
                            SiloClusterMode.PrimaryNode, 
                            _port.Port, 
                            initTimeOutSec: 15);
            
            Initialize();
        }

        private void Initialize()
        {
            Host = new TServiceHost();

            BasePort = ServiceArguments.BasePortOverride ?? GetBasePortFromHttpServiceAttribute();

            SiloStopped = Task.Run(() => Host.Run(ServiceArguments));

            //Silo is ready or failed to start
            Task.WaitAny(SiloStopped, Host.WaitForServiceStartedAsync());
            
            if (SiloStopped.IsFaulted)
            {
                try
                {
                    // Flatten Aggregated exception
                    SiloStopped.GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    throw new Exception("Silo Failed to start", e);
                }
            }
            else if (SiloStopped.IsCompleted)
                throw new Exception("Silo Failed to start");
        }

        protected int GetBasePortFromHttpServiceAttribute()
        {
            var commonConfig = new BaseCommonConfig();
            var mapper = new OrleansServiceInterfaceMapper(new AssemblyProvider(new ApplicationDirectoryProvider(commonConfig), commonConfig, new ConsoleLog()));
            var basePort = mapper.ServiceInterfaceTypes.First().GetCustomAttribute<HttpServiceAttribute>().BasePort;

            return basePort;
        }

        public override void Dispose()
        {
            _clusterClient?.Dispose();

            Host.Stop(); // don't use host.dispose, host.stop should do all the work

            var siloStopTimeout = TimeSpan.FromSeconds(60);
            
            var completed = SiloStopped.Wait(siloStopTimeout);

            if (!completed)
                throw new TimeoutException($"ServiceTester: The service failed to shutdown within the {siloStopTimeout.TotalSeconds} seconds limit.");

            var waitStopped = Host.WaitForServiceGracefullyStoppedAsync();

            // We aren't actually waiting?
            if (waitStopped.IsCompleted && waitStopped.Result == StopResult.Force)
                throw new TimeoutException("ServiceTester: The service failed to shutdown gracefully.");
           
            base.Dispose();
        }

        public IClusterClient GrainClient
        {
            get
            {
                InitGrainClient();
                return _clusterClient;
            }
        }

        protected virtual IClusterClient InitGrainClient()
        {
            if (_clusterClient == null)
            {
                lock (_locker)
                {
                    if (_clusterClient != null) 
                        return _clusterClient;

                    var gateways = new[]
                    {
                        new IPEndPoint( IPAddress.Loopback,  BasePort + (int) PortOffsets.SiloGateway),
                    };

                    var grainClientBuilder = new ClientBuilder();
                    
                    grainClientBuilder.UseStaticClustering(gateways);

                    grainClientBuilder
                        .Configure<ClusterOptions>(options =>
                        {
                            options.ClusterId = "dev";
                            options.ServiceId = "dev";
                        })
                        .Configure<SerializationProviderOptions>(options =>
                        {
                            options.SerializationProviders.Add(typeof(OrleansCustomSerialization));

                            if (_customSerializer != null)
                            {
                                // IF the custom serializer inherits the default one,
                                // replace it (as base class will supports all registered serialization types)
                                if(_customSerializer.IsSubclassOf(typeof(OrleansCustomSerialization)))
                                    options.SerializationProviders.Remove(typeof(OrleansCustomSerialization));
                                options.SerializationProviders.Add(_customSerializer);
                            }
                        });

                    var grainClient = grainClientBuilder.Build();

                    grainClient.Connect().GetAwaiter().GetResult();

                    _clusterClient = grainClient;
                }
            }
            
            return _clusterClient;
        }
    }


}