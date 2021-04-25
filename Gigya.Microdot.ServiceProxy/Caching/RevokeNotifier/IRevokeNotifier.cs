using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public interface IRevokeNotifier
    {
        void NotifyOnRevoke(object @this, Func<string, Task> callback, params string[] revokeKeys);
        void RemoveNotifications(object @this, params string[] revokeKeys);
        void RemoveAllNotifications(object @this);
    }
}
