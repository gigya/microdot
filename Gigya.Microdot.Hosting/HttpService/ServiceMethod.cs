using System;
using System.Reflection;
using System.Threading.Tasks;

using Gigya.Microdot.ServiceContract.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{
	/// <summary>
	/// A representation of a service method which contains the method to be invoked, and in Orleans, the grain interface type too.
	/// </summary>
    public class ServiceMethod
    {
		/// <summary>The type of the grain interface, used in Orleans to create a grain reference (not used elsewhere)
		/// In Orleans, it <b>MUST</b> be the grain interface (e.g. IDemoGrain).</summary>
		public Type GrainInterfaceType { get;  }

        /// <summary>
		/// The method which should be activated by IActivator. It <b>MUST</b> be a method on a type decorated by <see cref="HttpServiceAttribute" />.
		/// In Orleans, this is the service interface (e.g. IDemoService).
        /// </summary>
        public MethodInfo ServiceInterfaceMethod { get; }

		/// <summary>
		/// True if the method is compatible with TAP-based calling convention, otherwise false.
		/// </summary>
        public bool IsCompatible { get;  }

        internal ServiceMethod(Type grainInterfaceType, MethodInfo serviceInterfaceMethod)
        {
            GrainInterfaceType = grainInterfaceType;

            ServiceInterfaceMethod = serviceInterfaceMethod;

            IsCompatible = typeof(Task).IsAssignableFrom(serviceInterfaceMethod.ReturnType);
        }
    }
}