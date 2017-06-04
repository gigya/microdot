namespace Gigya.Microdot.ServiceProxy.Caching
{
    public interface ICachingProxyProvider<TInterface>
    {
        /// <summary>
        /// The instance of the actual data source, used when the data is not present in the cache.
        /// </summary>
        TInterface DataSource { get; }

        /// <summary>
        /// The instance of the transparent proxy used to access the data source with caching.
        /// </summary>
        /// <remarks>
        /// This is a thread-safe instance.
        /// </remarks>
        TInterface Proxy { get; }
    }
}