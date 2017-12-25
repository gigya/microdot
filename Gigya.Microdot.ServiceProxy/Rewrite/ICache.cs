using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceProxy.Caching;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
    interface IMemoizer : IProxyable, IDisposable
    {
        object Memoize(object dataSource, MethodInfo method, object[] args, CacheItemPolicyEx policy);
        object GetOrAdd(string key, Func<Task> factory, CacheItemPolicyEx policy);
    }
}
