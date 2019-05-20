using System.Net;
using System.Reflection;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.SharedLogic;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class OrleansConfigurationBuilder
    {
        private readonly OrleansConfig _orleansConfig;
        private readonly OrleansCodeConfig _commonConfig;
        private OrleansServiceInterfaceMapper _orleansServiceInterfaceMapper;
        private readonly ClusterIdentity _clusterIdentity;
        private IServiceEndPointDefinition _endPointDefinition;
        private ZooKeeperLogConsumer _zooKeeperLogConsumer;
        private readonly ServiceArguments _serviceArguments;

        private readonly ISiloHostBuilder _siloHostBuilder;
        public OrleansConfigurationBuilder(OrleansConfig orleansConfig, OrleansCodeConfig commonConfig,
            OrleansServiceInterfaceMapper orleansServiceInterfaceMapper,
            ClusterIdentity clusterIdentity, IServiceEndPointDefinition endPointDefinition,
            ZooKeeperLogConsumer zooKeeperLogConsumer, ServiceArguments serviceArguments)
        {
            _orleansConfig = orleansConfig;
            _commonConfig = commonConfig;
            _orleansServiceInterfaceMapper = orleansServiceInterfaceMapper;
            _clusterIdentity = clusterIdentity;
            _endPointDefinition = endPointDefinition;
            _zooKeeperLogConsumer = zooKeeperLogConsumer;
            _serviceArguments = serviceArguments;
            _siloHostBuilder = InitBuilder();
        }

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
                .Configure<SiloOptions>(options => options.SiloName = CurrentApplicationInfo.Name);//TODO should be not static !!
          
            //TODO in notificationsService change "UseReminder=ture" to RemindersSource sql and in the test inMemory

            if (_commonConfig.RemindersSource == OrleansCodeConfig.Reminders.Sql)
                silo.UseAdoNetReminderService(options => options.ConnectionString = _orleansConfig.MySql_v4_0.ConnectionString);
            if (_commonConfig.RemindersSource == OrleansCodeConfig.Reminders.InMemory)
                silo.UseInMemoryReminderService();


            // .UseSiloUnobservedExceptionsHandler(x=>)


            if (_serviceArguments.SiloClusterMode == SiloClusterMode.ZooKeeper)
            {
                silo.Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = _clusterIdentity.DeploymentId;
                    options.ServiceId = _clusterIdentity.ServiceId.ToString();
                }).UseZooKeeperClustering(options => options.ConnectionString = _orleansConfig.ZooKeeper.ConnectionString);

            }
            else
            {
                silo.UseLocalhostClustering()
                    .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Loopback);
                
                if (_serviceArguments.SiloClusterMode == SiloClusterMode.SecondaryNode)
                {

                }
                
            }

            return silo;
        }
    }
}