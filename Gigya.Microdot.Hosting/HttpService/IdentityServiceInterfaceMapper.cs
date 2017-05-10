using System;
using System.Linq;
using System.Reflection;

using Gigya.Microdot.ServiceContract.Exceptions;
using Gigya.Microdot.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.HttpService
{
	public class IdentityServiceInterfaceMapper : ServiceInterfaceMapper
	{	        
	    public IdentityServiceInterfaceMapper(Type serviceInterfaceType) : this(new[] { serviceInterfaceType }) { }


		public IdentityServiceInterfaceMapper(Type[] serviceInterfaceTypes)
		{
		    var invalidInterfaces = serviceInterfaceTypes.Where(i => i.IsInterface == false || i.GetCustomAttribute<HttpServiceAttribute>() == null);

            if (invalidInterfaces.Any())
                throw new ProgrammaticException("The following service interface types are invalid, please make sure the types specified are interfaces and are decorated with [HttpService] attribute: " + string.Join(", ", invalidInterfaces));

			ServiceInterfaceTypes = serviceInterfaceTypes;

		    ExtractHealthStatusServiceType(serviceInterfaceTypes);            
		}


		public override Type GetGrainInterface(Type serviceInterface)
		{
			return serviceInterface;
		}
	}
}