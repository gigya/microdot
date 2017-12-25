using System.Reflection;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
    public interface IProxyable
    {
        object Invoke(MethodInfo targetMethod, object[] args);
    }
}