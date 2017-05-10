namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// Scope where this service should be used
    /// </summary>
    public enum ServiceScope
    {
        /// This Service is in entire DataCenter scope, and should get requests from any environment within this data-center.
        DataCenter,

        /// This Service is in Environment scope, and should get requests only from other services in same environment and same data-center.
        Environment
    }
}