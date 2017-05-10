namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// The discovery mode to use, e.g. whether to use DNS resolving, Consul, etc.
    /// </summary>
    public enum DiscoverySource
    {
        Config,
        Consul,
        Local
    }
}