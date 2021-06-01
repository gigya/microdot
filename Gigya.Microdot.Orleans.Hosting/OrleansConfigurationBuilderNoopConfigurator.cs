using Orleans.Hosting;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class OrleansConfigurationBuilderNoopConfigurator : IOrleansConfigurationBuilderConfigurator
    {
        public void PreInitializationConfiguration(SiloHostBuilder siloHostBuilder)
        {
        }

        public void PostInitializationConfiguration(SiloHostBuilder siloHostBuilder)
        {
        }
    }
}