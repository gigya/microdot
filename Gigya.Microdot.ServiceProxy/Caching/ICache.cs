using System.Runtime.Caching;

using Nito.AsyncEx;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public interface ICache
    {
        AsyncLazy<T> GetOrAdd<T>(string key, T value, CacheItemPolicy policy);
        void Clear();
    }
}