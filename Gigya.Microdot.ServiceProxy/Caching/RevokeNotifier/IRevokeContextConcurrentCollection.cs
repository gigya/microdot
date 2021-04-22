using System.Collections.Generic;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public interface IRevokeContextConcurrentCollection : IEnumerable<RevokeContext>
    {
        bool IsEmpty { get;}
        IRevokeContextConcurrentCollection MergeMissingEntriesWith(IRevokeContextConcurrentCollection other);
        void Insert(RevokeContext context);
        bool RemoveEntryMatchingObject(object obj);

        int Cleanup();
    }



}