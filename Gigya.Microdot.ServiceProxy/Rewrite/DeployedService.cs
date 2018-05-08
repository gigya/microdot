using System;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic.HttpService.Schema;
using Gigya.Microdot.SharedLogic.Utils;
using Nito.AsyncEx;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
    internal class DeployedService : IDisposable
    {
        public ServiceSchema Schema { get; set; }
        public IMemoizer Memoizer { get; set; }
        public ILoadBalancer LoadBalancer { get; set; }
        public AsyncLock Lock { get; } = new AsyncLock();

        public void Dispose()
        {
            Memoizer.TryDispose();
            LoadBalancer.TryDispose();
        }
    }
}