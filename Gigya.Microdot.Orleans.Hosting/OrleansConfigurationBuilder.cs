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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.SharedLogic;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;

namespace Gigya.Microdot.Orleans.Hosting
{
    //TODO:  Support MembershipTableGrain, StorageProvider, interceptor, UseSiloUnobservedExceptionsHandler??, StatisticsOptions
    public class OrleansConfigurationBuilder
    {
        private readonly OrleansConfig _orleansConfig;
        private readonly OrleansCodeConfig _commonConfig;
        private OrleansServiceInterfaceMapper _orleansServiceInterfaceMapper;
        private readonly ClusterIdentity _clusterIdentity;
        private IServiceEndPointDefinition _endPointDefinition;
        private readonly ServiceArguments _serviceArguments;

        private readonly ISiloHostBuilder _siloHostBuilder;
        public OrleansConfigurationBuilder(OrleansConfig orleansConfig, OrleansCodeConfig commonConfig,
            OrleansServiceInterfaceMapper orleansServiceInterfaceMapper,
            ClusterIdentity clusterIdentity, IServiceEndPointDefinition endPointDefinition,
             ServiceArguments serviceArguments)
        {
            _orleansConfig = orleansConfig;
            _commonConfig = commonConfig;
            _orleansServiceInterfaceMapper = orleansServiceInterfaceMapper;
            _clusterIdentity = clusterIdentity;
            _endPointDefinition = endPointDefinition;
            _serviceArguments = serviceArguments;
            _siloHostBuilder = InitBuilder();
        }

        /// <summary> Let you control all Orleans Options:
        ///  ClientMessagingOptions
        ///  ClusterMembershipOptions
        ///  ClusterOptions
        ///  ClusterOptionsValidator
        ///  CollectionAgeLimitAttribute
        ///  GatewayOptions
        ///  GrainVersioningOptions
        ///  HashRingStreamQueueMapperOptions
        ///  LoadSheddingOptions
        ///  MessagingOptions
        ///  MultiClusterOptions
        ///  NetworkingOptions
        ///  OptionConfigureExtensionMethods
        ///  PerformanceTuningOptions
        ///  SerializationProviderOptions
        ///  ServiceCollectionExtensions
        ///  SimpleMessageStreamProviderOptions
        ///  StaticGatewayListProviderOptions
        ///  StatisticsOptions
        ///  StreamLifecycleOptions
        ///  StreamPubSubOptions
        ///  StreamPullingAgentOptions
        ///  TelemetryOptions
        ///  TelemetryOptionsExtensions
        ///  TypeManagementOptions
        /// </summary>
        /// <returns></returns>


        public ISiloHostBuilder GetBuilder()
        {
            return _siloHostBuilder;
        }

        private ISiloHostBuilder InitBuilder()
        {

            var silo = new SiloHostBuilder()

                .Configure<SerializationProviderOptions>(options =>
                {
                    options.SerializationProviders.Add(typeof(OrleansCustomSerialization));
                    options.FallbackSerializationProvider = typeof(OrleansCustomSerialization);
                })

                .ConfigureApplicationParts(parts => parts.AddApplicationPart(Assembly.GetEntryAssembly()).WithReferences())

                .Configure<SiloOptions>(options => options.SiloName = CurrentApplicationInfo.Name)//TODO should be not static !!

                .Configure<EndpointOptions>(options =>
            {
                options.AdvertisedIPAddress = IPAddress.Loopback;
                options.GatewayPort = _endPointDefinition.SiloNetworkingPort;
            });

            SetGrainCollectionOptions(silo);

            silo.Configure<PerformanceTuningOptions>(options =>
            {
                options.DefaultConnectionLimit = ServicePointManager.DefaultConnectionLimit;
            });

            silo.Configure<SchedulingOptions>(options =>
            {
                options.PerformDeadlockDetection = true;
                options.AllowCallChainReentrancy = true;
                options.MaxActiveThreads = Process.GetCurrentProcess().ProcessorAffinityList().Count();

            });


            silo.Configure<ClusterMembershipOptions>(options =>
            {
                options.ExpectedClusterSize = 1; // Minimizes artificial startup delay to a maximum of 0.5 seconds (instead of 10 seconds)
            });

            silo.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = _clusterIdentity.DeploymentId;
                options.ServiceId = _clusterIdentity.ServiceId.ToString();
            });


            //TODO in notificationsService change "UseReminder=ture" to RemindersSource sql and in the test inMemory

            if (_commonConfig.RemindersSource == OrleansCodeConfig.Reminders.Sql)
                silo.UseAdoNetReminderService(options => options.ConnectionString = _orleansConfig.MySql_v4_0.ConnectionString);
            if (_commonConfig.RemindersSource == OrleansCodeConfig.Reminders.InMemory)
                silo.UseInMemoryReminderService();


            switch (_serviceArguments.SiloClusterMode)
            {
                case SiloClusterMode.ZooKeeper:
                    silo.UseZooKeeperClustering(options =>
                    {
                        options.ConnectionString = _orleansConfig.ZooKeeper.ConnectionString;
                    });
                    break;
                case SiloClusterMode.Unspecified:
                    silo.UseLocalhostClustering();
                    //TODO:  Support MembershipTableGrain
                    break;
                case SiloClusterMode.PrimaryNode:
                    silo.UseLocalhostClustering();

                    break;
                case SiloClusterMode.SecondaryNode:
                    silo.UseLocalhostClustering();

                    break;
            }

            return silo;
        }

        private void SetGrainCollectionOptions(ISiloHostBuilder silo)
        {
            silo.Configure<GrainCollectionOptions>(options =>
            {
                options.CollectionAge = TimeSpan.FromMinutes(_orleansConfig.DefaultGrainAgeLimitInMins);
                if (_orleansConfig.GrainAgeLimits != null)
                {
                    foreach (var grainAgeLimitConfig in _orleansConfig.GrainAgeLimits.Values)
                    {
                        try
                        {
                            _orleansServiceInterfaceMapper.ServiceClassesTypes.Single(x =>
                                x.FullName.Equals(grainAgeLimitConfig.GrainType));
                            options.ClassSpecificCollectionAge.Add(grainAgeLimitConfig.GrainType,
                                TimeSpan.FromMinutes(grainAgeLimitConfig.GrainAgeLimitInMins));
                        }
                        catch (Exception e)
                        {
                            throw new ArgumentException(
                                $"Assigning Age Limit on {grainAgeLimitConfig.GrainType} has failed, because {grainAgeLimitConfig.GrainType} is an invalid type\n{e.Message}");
                        }
                    }
                }
            });
        }
    }
}