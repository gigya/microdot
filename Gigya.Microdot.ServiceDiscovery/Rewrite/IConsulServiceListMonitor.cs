using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors Consul using KeyValue api, caching the list of all available services and exposing <see cref="ServiceExists"/>.
    /// This saves each instance of <see cref="ConsulNodeMonitor"/> (per service per env) from having to long-poll Consul.
    /// </summary>    
    public interface IConsulServiceListMonitor: IDisposable
    {
        Task Init();

        bool ServiceExists(DeploymentIdentifier deploymentId, out DeploymentIdentifier normalizedDeploymentId);
    }
}