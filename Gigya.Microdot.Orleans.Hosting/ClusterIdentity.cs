using System;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>Provides information about services in this silo. /// </summary>
    public class ClusterIdentity
    {
        /// <summary>
        /// Provides the ServiceId this orleans cluster is running as.
        /// ServiceId's are intended to be long lived Id values for a particular service which will remain constant 
        /// even if the service is started / redeployed multiple times during its operations life.
        /// </summary>
        public Guid ServiceId { get; }

        /// <summary>
        /// Provides the DeploymentId for this orleans cluster.
        /// </summary>
        public string DeploymentId { get; }


        /// <summary>
        /// Performs discovery of services in the silo and populates the class' static members with information about them.
        /// </summary>
        public ClusterIdentity(ServiceArguments serviceArguments, ILog log, IEnvironmentVariableProvider environmentVariableProvider)
        {
            if (serviceArguments.SiloClusterMode != SiloClusterMode.ZooKeeper)
                return;

            string dc = environmentVariableProvider.DataCenter;
            string env = environmentVariableProvider.DeploymentEnvironment;

            if (dc == null || env == null)
                throw new EnvironmentException("One or more of the following environment variables, which are required when running with ZooKeeper, have not been set: %DC%, %ENV%");

            var serviceIdSourceString = string.Join("_", dc, env, CurrentApplicationInfo.Name, CurrentApplicationInfo.InstanceName);
            ServiceId = Guid.Parse(serviceIdSourceString.GetHashCode().ToString("X32"));

            DeploymentId = serviceIdSourceString + "_" + CurrentApplicationInfo.Version;

            log.Info(_ => _("Orleans Cluster Identity Information (see tags)", unencryptedTags: new { OrleansDeploymentId = DeploymentId, OrleansServiceId = ServiceId, serviceIdSourceString }));
        }
    }
}