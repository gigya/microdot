using System;
using System.Reflection;
using System.Reflection.DispatchProxy;


namespace Gigya.Microdot.ServiceProxy
{

    /// <summary>
    /// A helper class used to redirect <see cref="DispatchProxy"/> calls to another location.
    /// </summary>
    /// <remarks>
    /// In order for proxy generation to succeed, this class must be public and have a parameterless constructor.
    /// </remarks>
    public class DelegatingDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> InvokeDelegate { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (InvokeDelegate == null)
                throw new InvalidOperationException($"You cannot use the {nameof(DelegatingDispatchProxy)} class without initializing its {nameof(InvokeDelegate)} property after instantiation.");

            return InvokeDelegate(targetMethod, args);
        }
    }
}