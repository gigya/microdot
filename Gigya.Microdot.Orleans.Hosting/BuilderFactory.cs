using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.SharedLogic;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;

namespace Gigya.Microdot.Orleans.Hosting
{
    class BuilderFactory
    {
        private OrleansConfig OrleansConfig { get; }
        private OrleansCodeConfig CommonConfig { get; }
        private OrleansServiceInterfaceMapper OrleansServiceInterfaceMapper { get; }
        private ClusterIdentity ClusterIdentity { get; }
        private IServiceEndPointDefinition EndPointDefinition { get; }
        private ZooKeeperLogConsumer ZooKeeperLogConsumer { get; }
        private ServiceArguments ServiceArguments { get; }

        public BuilderFactory(OrleansConfig orleansConfig, OrleansCodeConfig commonConfig,
            OrleansServiceInterfaceMapper orleansServiceInterfaceMapper,
            ClusterIdentity clusterIdentity, IServiceEndPointDefinition endPointDefinition,
            ZooKeeperLogConsumer zooKeeperLogConsumer, ServiceArguments serviceArguments)
        {
            OrleansConfig = orleansConfig;
            CommonConfig = commonConfig;
            OrleansServiceInterfaceMapper = orleansServiceInterfaceMapper;
            ClusterIdentity = clusterIdentity;
            EndPointDefinition = endPointDefinition;
            ZooKeeperLogConsumer = zooKeeperLogConsumer;
            ServiceArguments = serviceArguments;
        }

        public ISiloHostBuilder BuildSilo()
        {

            var silo = new SiloHostBuilder()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "my-first-cluster";
                    options.ServiceId = "AspNetSampleApp";
                })
                .UseLocalhostClustering()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dev";
                    options.ServiceId = "HelloWorldApp";

                })
                .Configure<SiloOptions>(options => options.SiloName = "name")
                .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback)

                .Configure<SerializationProviderOptions>(options =>
                {
                    options.SerializationProviders.Add(typeof(OrleansCustomSerialization));
                    options.FallbackSerializationProvider = typeof(OrleansCustomSerialization);
                })
                .ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(Assembly.GetEntryAssembly()).WithReferences();
                });

            return silo;
        }
    }

    public class OrleansConfig : IConfigObject
    {
        public string MetricsTableWriteInterval { get; set; } = "00:00:01";
        public double DefaultGrainAgeLimitInMins { get; set; } = 30;
        public IDictionary<string, GrainAgeLimitConfig> GrainAgeLimits { get; set; } = new ConcurrentDictionary<string, GrainAgeLimitConfig>();

        public ZooKeeperConfig ZooKeeper { get; set; }

        public MySqlConfig MySql_v4_0 { get; set; }

    }


    public class ZooKeeperConfig
    {
        public string ConnectionString { get; set; }
    }

    public class MySqlConfig
    {
        public string ConnectionString { get; set; }
    }


    public class GrainAgeLimitConfig
    {
        public double GrainAgeLimitInMins { get; set; }
        public string GrainType { get; set; }

    }

}
