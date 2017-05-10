using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class AsyncCacheItem
    {
        public object Lock { get; } = new object();
        public DateTime NextRefreshTime { get; set; }
        public Task<object> CurrentValueTask { get; set; }
        public Task RefreshTask { get; set; }
    }
}