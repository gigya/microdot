using System;

namespace Gigya.ServiceContract.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter| AttributeTargets.Method)]

    public class SensitiveAttribute : Attribute
    {
        public bool Secretive { get; set; }
    }
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method)]

    public class NonSensitiveAttribute : Attribute{}

}
