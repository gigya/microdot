namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors Consul using Query api, to find whether service is deployed, and get its nodes list
    /// </summary>
    public interface IQueryBasedConsulNodeMonitor: IConsulNodeMonitor
    {
        /// <summary>
        /// Whether this service appears on Consul as a deployed service
        /// </summary>
        bool IsDeployed { get; }
    }
}