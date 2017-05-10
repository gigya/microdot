using System;

namespace Gigya.Microdot.ServiceContract.Attributes
{
    /// <summary>
    /// Specifies that the method should be cached using the CachingProxy. This attribute should only be applied to
    /// methods on interfaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CachedAttribute : Attribute { }
}
