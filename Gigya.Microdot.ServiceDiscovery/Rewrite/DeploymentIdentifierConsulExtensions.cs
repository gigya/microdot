using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public static class DeploymentIdentifierConsulExtensions
    {
        /// <summary>
        /// Returns the service name as it is used by Consul, e.g. "ServiceName-env"
        /// </summary>
        public static string GetConsulServiceName(this DeploymentIdentifier deploymentIdentifier)
        {
            return deploymentIdentifier.IsEnvironmentSpecific ?
                $"{deploymentIdentifier.ServiceName}-{deploymentIdentifier.DeploymentEnvironment}" :
                deploymentIdentifier.ServiceName;
        }

        public static string GetConsulDataCenter(this DeploymentIdentifier deploymentIdentifier)
        {
            return deploymentIdentifier.DataCenter;
        }
    }
}
