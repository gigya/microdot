using System;

namespace Gigya.ServiceContract.Attributes
{
    /// <summary>Mark the parameter as containing log field data
    /// by providing this attribute the class will be dissects into properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class LogFieldsAttribute : Attribute { }
}