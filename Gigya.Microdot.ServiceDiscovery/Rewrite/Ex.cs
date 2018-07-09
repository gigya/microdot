using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    internal static class Ex
    {

        public static ConfigurationException IncorrectHostFormatInConfig(string hosts, string serviceName)
        {
            return new ConfigurationException("A config-specified hostname name must contain at most one colon (:).",
                unencrypted: new Tags
                {
                    { "hosts", hosts },
                    { "configPath", $"Discovery.{serviceName}.Hosts" },
                });
        }

        public static EnvironmentException ServiceNotDeployed(string dc, DeploymentIdentifier deploymentIdentifier)
        {
            return new EnvironmentException(
                "The requested service is not deployed in the specified data center and environment. See tags for details.", 
                unencrypted: new Tags
                {
                    { "dc", dc },
                    { "serviceName", deploymentIdentifier.ServiceName },
                    { "serviceEnv", deploymentIdentifier.DeploymentEnvironment },
                });
        }
    }
}
