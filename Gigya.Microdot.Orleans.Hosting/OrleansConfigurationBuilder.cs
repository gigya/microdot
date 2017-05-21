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
using System.Diagnostics;
using System.Net;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.SharedLogic;
using org.apache.zookeeper;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class ZooKeeperConfig 
    {
        public string ConnectionString { get; set; }
    }

    public class MySqlConfig
    {
        public string ConnectionString { get; set; }
    }

    public class OrleansConfig:IConfigObject
    {
        public int MaxActiveThreads { get; set; }

        public string MetricsTableWriteInterval { get; set; } = "00:00:01";

        public ZooKeeperConfig ZooKeeper { get; set; }

        public MySqlConfig MySql_v4_0 { get; set; }
    }

	public class OrleansConfigurationBuilder
	{
	    public ClusterConfiguration ClusterConfiguration { get; }
        public Silo.SiloType SiloType { get; private set; }


	    public OrleansConfigurationBuilder(OrleansConfig orleansConfig, OrleansCodeConfig commonConfig,
	                                       ClusterConfiguration clusterConfiguration, ClusterIdentity clusterIdentity, IServiceEndPointDefinition endPointDefinition,
	                                       OrleansLogConsumer orleansLogConsumer, ZooKeeperLogConsumer zooKeeperLogConsumer)
	    {
	        ClusterConfiguration = clusterConfiguration;

	        SiloType = Silo.SiloType.Secondary;
	        var globals = ClusterConfiguration.Globals;
	        var defaults = ClusterConfiguration.Defaults;
	        globals.ExpectedClusterSize = 1; // Minimizes artificial startup delay to a maximum of 0.5 seconds (instead of 10 seconds).
	        globals.RegisterBootstrapProvider<DelegatingBootstrapProvider>(nameof(DelegatingBootstrapProvider));
	        defaults.ProxyGatewayEndpoint = new IPEndPoint(IPAddress.Loopback, endPointDefinition.SiloGatewayPort);
	        defaults.Port = endPointDefinition.SiloNetworkingPort;

	        if(orleansConfig.MaxActiveThreads > 0)
	            defaults.MaxActiveThreads = orleansConfig.MaxActiveThreads;

	        // Orleans log redirection
	        defaults.TraceToConsole = false;
	        defaults.TraceFileName = null;
	        defaults.TraceFilePattern = null;
	        LogManager.LogConsumers.Add(orleansLogConsumer);

	        // ZooKeeper log redirection
	        ZooKeeper.LogToFile = false;
	        ZooKeeper.LogToTrace = false;
	        ZooKeeper.LogLevel = TraceLevel.Verbose;
	        ZooKeeper.CustomLogConsumer = zooKeeperLogConsumer;

	        //Setup Statistics
	        var metricsProviderType = typeof(MetricsStatisticsPublisher);
	        globals.ProviderConfigurations.Add("Statistics", new ProviderCategoryConfiguration("Statistics")
	        {
	            Providers = new Dictionary<string, IProviderConfiguration>
	            {
	                {
	                    metricsProviderType.Name,
	                    new ProviderConfiguration(new Dictionary<string, string>(), metricsProviderType.FullName, metricsProviderType.Name)
	                }
	            }
	        });
	        defaults.StatisticsProviderName = metricsProviderType.Name;
	        defaults.StatisticsCollectionLevel = StatisticsLevel.Info;
	        defaults.StatisticsLogWriteInterval = TimeSpan.Parse(orleansConfig.MetricsTableWriteInterval);
	        defaults.StatisticsWriteLogStatisticsToTable = true;

	        if(commonConfig.ServiceArguments.SiloClusterMode != SiloClusterMode.ZooKeeper)
	        {
	            defaults.HostNameOrIPAddress = "localhost";
	            globals.ReminderServiceType = commonConfig.UseReminders
	                ? GlobalConfiguration.ReminderServiceProviderType.ReminderTableGrain
	                :GlobalConfiguration.ReminderServiceProviderType.Disabled;

	            globals.LivenessType = GlobalConfiguration.LivenessProviderType.MembershipTableGrain;

	            if(commonConfig.ServiceArguments.SiloClusterMode == SiloClusterMode.PrimaryNode)
	            {
	                globals.SeedNodes.Add(new IPEndPoint(IPAddress.Loopback, endPointDefinition.SiloNetworkingPort));
	                SiloType = Silo.SiloType.Primary;
	            }
	            else
	            {
	                globals.SeedNodes.Add(new IPEndPoint(IPAddress.Loopback, endPointDefinition.SiloNetworkingPortOfPrimaryNode));
	            }
	        }
	        else
	        {
	            globals.DeploymentId = clusterIdentity.DeploymentId;
	            globals.LivenessType = GlobalConfiguration.LivenessProviderType.ZooKeeper;
	            globals.DataConnectionString = orleansConfig.ZooKeeper.ConnectionString;

	            if(commonConfig.UseReminders)
	            {
	                globals.ServiceId = clusterIdentity.ServiceId;
	                globals.ReminderServiceType = GlobalConfiguration.ReminderServiceProviderType.SqlServer;
	                globals.DataConnectionStringForReminders = orleansConfig.MySql_v4_0.ConnectionString;
	                globals.AdoInvariantForReminders = "MySql.Data.MySqlClient";
	            }
	            else
	                globals.ReminderServiceType = GlobalConfiguration.ReminderServiceProviderType.Disabled;
	        }

	        if(string.IsNullOrEmpty(commonConfig.StorageProviderTypeFullName)==false)
	        {
	            globals.RegisterStorageProvider(commonConfig.StorageProviderTypeFullName, "Default");
	            globals.RegisterStorageProvider(commonConfig.StorageProviderTypeFullName, commonConfig.StorageProviderName);
	        }
	    }
	}
}