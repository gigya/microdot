using System;
using System.Collections.Generic;
using System.Text;
using Orleans.Hosting;

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>
    /// This interface is intended to act as a hook for configuring Orleans host builder 
    /// </summary>
    public interface IOrleansConfigurationBuilderConfigurator
    {
        /// <summary>
        /// Configure the host builder before it is initialized by microdot
        /// </summary>
        /// <param name="siloHostBuilder"></param>
        void PreInitializationConfiguration(SiloHostBuilder siloHostBuilder);

        /// <summary>
        /// Configure the host builder after it is initialized by microdot
        /// </summary>
        /// <param name="siloHostBuilder"></param>
        void PostInitializationConfiguration(SiloHostBuilder siloHostBuilder);
    }
}
