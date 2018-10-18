using Gigya.Microdot.ServiceProxy.Rewrite;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public interface ICachingProxyProvider: IProxyable
    {
        /// <summary>
        /// The instance of the actual data source, used when the data is not present in the cache.
        /// </summary>
        object DataSource { get; }

    }
}