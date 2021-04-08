using System.Collections.Generic;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public interface IRevokeKeyIndexer
    {
        IEnumerable<RevokeContext> GetLiveRevokeesAndSafelyRemoveDeadOnes(string revokeKey);
        void AddRevokeContext(string key, RevokeContext newContext);
        bool Remove(object @this, string key);
        void Remove(object @this);
        void Cleanup();
    }
}
