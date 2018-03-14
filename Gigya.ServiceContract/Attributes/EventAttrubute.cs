using System;

namespace Gigya.ServiceContract.Attributes
{
    /// <summary>Mark the parameter as containing sensitive data.
    /// When sensitive data is automaticity logged (e.g. event publisher) it will be encrypted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter| AttributeTargets.Method | AttributeTargets.Property)]

    public class SensitiveAttribute : Attribute
    {
        /// <summary>Mark the parameter as containing Secretive data.
        ///it will never log automaticity (e.g. event publisher).
        /// </summary>
        public bool Secretive { get; set; }
    }

    /// <summary>Mark the parameter as containing nonsensitive data.
    /// When nonsensitive data is automaticity logged (e.g. event publisher) it wont be encrypted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method)]
    public class NonSensitiveAttribute : Attribute{}


    /// <summary>Mark the parameter as containing log field data
    /// by providing this attribute the class will be dissects into properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class LogFieldsAttribute : Attribute{}




}
