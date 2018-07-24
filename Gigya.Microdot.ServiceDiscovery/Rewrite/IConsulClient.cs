using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Client for accessing Consul
    /// </summary>
    public interface IConsulClient: IDisposable
    {
        /// <summary>
        /// Get all keys from the Consul's key-value store
        /// </summary>
        /// <param name="modifyIndex">The modifyIndex of last response from Consul, to be used for long-polling. Should be zero (0) When calling Consul for the first time</param>
        /// <param name="folder">folder of key-value store (e.g. "service", "flags")</param>
        /// <param name="cancellationToken">Token for cancelling the call to Consul</param>
        /// <returns></returns>
        Task<ConsulResponse<string[]>> GetAllKeys(ulong modifyIndex, string folder, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">Type to be deserialized (Json) when reading the key value from Consul</typeparam>
        /// <param name="modifyIndex">The modifyIndex of last response from Consul, to be used for long-polling. Should be zero (0) When calling Consul for the first time</param>
        /// <param name="folder">folder of key-value store (e.g. "service", "flags")</param>
        /// <param name="zone">zone where the key-value is requested. Null if key-value is requested in current zone</param>
        /// <param name="cancellationToken">Token for cancelling the call to Consul</param>        
        Task<ConsulResponse<T>> GetKey<T>(ulong modifyIndex, string folder, string key, string zone = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class;
    }
}
