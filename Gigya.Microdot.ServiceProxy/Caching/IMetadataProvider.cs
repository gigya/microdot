using System;
using System.Reflection;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public interface IMetadataProvider
    {
        Type GetMethodTaskResultType(MethodInfo method);
        bool IsCached(MethodInfo methodInfo);
        bool HasCachedMethods(Type interfaceType);
    }
}