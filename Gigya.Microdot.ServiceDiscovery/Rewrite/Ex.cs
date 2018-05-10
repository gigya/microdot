using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    internal static class Ex
    {
        public static ServiceUnreachableException ZeroNodesInConfig(string serviceName)
        {
            return new ServiceUnreachableException(
                "No nodes were specified in the configuration for the requested service. Please make sure you've specified a list of " + 
                "hosts for the requested service in the configuration. If you're a developer and want to access a service on your local " +
                "machine, change service configuration to Discovery.[requestedService].Mode=\"Local\". See tags for the name of the " +
                "service requested, and for the configuration path where the list of nodes are expected to be specified.",
                unencrypted: new Tags
                {
                    { "requestedService", serviceName },
                    { "missingConfigPath", $"Discovery.{serviceName}.Hosts" }
                });
        }

        public static ConfigurationException IncorrectHostFormatInConfig(string hosts)
        {
            return new ConfigurationException("A config-specified hostname name must contain at most one colon (:).",
                unencrypted: new Tags
                {
                    { "hosts", hosts }
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
                    { "serviceEnv", deploymentIdentifier.DeploymentEnvironment }
                });
        }
    }
}
