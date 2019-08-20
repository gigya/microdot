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

using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.SharedLogic;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime.Configuration;
using Orleans.Statistics;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Orleans.Providers;
using Orleans;


namespace Gigya.Microdot.Orleans.Hosting
{
    //TODO:  Support   UseSiloUnobservedExceptionsHandler??
    public class OrleansConfigurationBuilder
    {
        private readonly OrleansConfig _orleansConfig;
        private readonly OrleansCodeConfig _commonConfig;
        private readonly OrleansServiceInterfaceMapper _orleansServiceInterfaceMapper;
        private readonly ClusterIdentity _clusterIdentity;
        private readonly IServiceEndPointDefinition _endPointDefinition;
        private readonly ServiceArguments _serviceArguments;
        private readonly CurrentApplicationInfo _appInfo;
        private readonly ISiloHostBuilder _siloHostBuilder;

        public OrleansConfigurationBuilder(OrleansConfig orleansConfig, OrleansCodeConfig commonConfig,
            OrleansServiceInterfaceMapper orleansServiceInterfaceMapper,
            ClusterIdentity clusterIdentity, IServiceEndPointDefinition endPointDefinition,
            ServiceArguments serviceArguments,
            CurrentApplicationInfo appInfo)
        {
            _orleansConfig = orleansConfig;
            _commonConfig = commonConfig;
            _orleansServiceInterfaceMapper = orleansServiceInterfaceMapper;
            _clusterIdentity = clusterIdentity;
            _endPointDefinition = endPointDefinition;
            _serviceArguments = serviceArguments;
            _appInfo = appInfo;
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
        public ISiloHostBuilder GetBuilder()
        {
            return _siloHostBuilder;
        }

        private ISiloHostBuilder InitBuilder()
        {
            var hostBuilder = new SiloHostBuilder();

            hostBuilder.Configure<SerializationProviderOptions>(options =>
                {
                    options.SerializationProviders.Add(typeof(OrleansCustomSerialization));
                    
                    // A workaround for an Orleans issue
                    // to ensure the stack trace properly de/serialized
                    // Gigya.Microdot.UnitTests.Serialization.ExceptionSerializationTests
                    options.SerializationProviders.Add(typeof(HttpRequestExceptionSerializer));

                    options.FallbackSerializationProvider = typeof(OrleansCustomSerialization);
                })
                .UsePerfCounterEnvironmentStatistics()
                // We paid attention that AddFromApplicationBaseDirectory making issues of non-discovering grain types.
                .ConfigureApplicationParts(parts => parts.AddFromAppDomain())
                .Configure<SiloOptions>(options => options.SiloName = _appInfo.Name);

            if (_orleansConfig.Dashboard.Enable)
            {
                hostBuilder.UseDashboard(o =>
                    {
                        o.Port = _endPointDefinition.SiloDashboardPort;
                        o.CounterUpdateIntervalMs = (int)TimeSpan.Parse(_orleansConfig.Dashboard.WriteInterval).TotalMilliseconds;
                        o.HideTrace = _orleansConfig.Dashboard.HideTrace;
                    });
            }

            SetGrainCollectionOptions(hostBuilder);

            hostBuilder.Configure<PerformanceTuningOptions>(options =>
            {
                options.DefaultConnectionLimit = ServicePointManager.DefaultConnectionLimit;
            });
            hostBuilder.AddMemoryGrainStorage(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, options => options.NumStorageGrains = 10);
            hostBuilder.Configure<TelemetryOptions>(o=>o.AddConsumer<MetricsStatisticsConsumer>());
            hostBuilder.Configure<SchedulingOptions>(options =>
            {
                options.PerformDeadlockDetection = true;
                options.AllowCallChainReentrancy = true;
                options.MaxActiveThreads = Process.GetCurrentProcess().ProcessorAffinityList().Count();
            });

            hostBuilder.Configure<ClusterMembershipOptions>(options =>
            {
                // Minimizes artificial startup delay to a maximum of 0.5 seconds (instead of 10 seconds)
                options.ExpectedClusterSize = 1;
            });

            SetReminder(hostBuilder);
            SetSiloSource(hostBuilder);

            hostBuilder.Configure<StatisticsOptions>(o =>
            {
                o.LogWriteInterval = TimeSpan.FromDays(1);
                o.PerfCountersWriteInterval = TimeSpan.Parse(_orleansConfig.MetricsTableWriteInterval);
            });

            return hostBuilder;
        }

        private void SetReminder(ISiloHostBuilder silo)
        {
            if (_commonConfig.RemindersSource == OrleansCodeConfig.Reminders.Sql)
                silo.UseAdoNetReminderService(options => options.ConnectionString = _orleansConfig.MySql_v4_0.ConnectionString);
            if (_commonConfig.RemindersSource == OrleansCodeConfig.Reminders.InMemory)
                silo.UseInMemoryReminderService();
        }

        private void SetSiloSource(ISiloHostBuilder silo)
        {
            switch (_serviceArguments.SiloClusterMode)
            {
                case SiloClusterMode.ZooKeeper:
                    silo.UseZooKeeperClustering(options =>
                    {
                        options.ConnectionString = _orleansConfig.ZooKeeper.ConnectionString;
                    }).ConfigureEndpoints(siloPort: _endPointDefinition.SiloNetworkingPort, gatewayPort: _endPointDefinition.SiloGatewayPort)
                   .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = _clusterIdentity.DeploymentId;
                        options.ServiceId = _clusterIdentity.ServiceId.ToString();
                    });
                    break;

                case SiloClusterMode.Unspecified:
                case SiloClusterMode.PrimaryNode:
                    silo.UseLocalhostClustering(_endPointDefinition.SiloNetworkingPort, _endPointDefinition.SiloGatewayPort);
                    break;

                case SiloClusterMode.SecondaryNode:
                    if(_endPointDefinition.SiloNetworkingPortOfPrimaryNode == null)
                        throw new ArgumentException($"missing {nameof(_endPointDefinition.SiloNetworkingPortOfPrimaryNode)}");

                    silo.UseLocalhostClustering(_endPointDefinition.SiloNetworkingPort, _endPointDefinition.SiloNetworkingPortOfPrimaryNode.Value);

                    break;
            }
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

        /// <summary>
        /// Configure custom serializer in addition to the default one (in terms of types it expected to serialize and settings used).
        /// </summary>
        /// <param name="remove">The serializer type to remove. Use typeof(OrleansCustomSerialization) if type inheriting from it, otherwise use null.</param>
        /// <param name="add">The serializer type to add. The best practice is inherit from <see cref="OrleansCustomSerialization"/>.</param>
        /// <exception cref="InvalidOperationException">If type to remove wasn't previously added.</exception>
        public void ReplaceSerializationProvider(Type remove, Type add)
        {
            _siloHostBuilder.Configure<SerializationProviderOptions>(options =>
            {
                if (remove != null)
                {
                    var toRemove = options.SerializationProviders.SingleOrDefault(t => t == remove);
                    if(toRemove != null)
                        options.SerializationProviders.Remove(toRemove);
                    else
                        throw new InvalidOperationException("Not found in the SerializationProviders types. Can't delete.");
                }

                options.SerializationProviders.Add(add);
            });
        }
    }
}