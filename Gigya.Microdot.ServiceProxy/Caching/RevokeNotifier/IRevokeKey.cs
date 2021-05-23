using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public interface IRevokeKey
    {
        Task OnKeyRevoked(string key);
    }
}