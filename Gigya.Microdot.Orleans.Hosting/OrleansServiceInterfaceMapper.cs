using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.SharedLogic;

using Orleans;


namespace Gigya.Microdot.Orleans.Hosting
{
    
    public class OrleansServiceInterfaceMapper : ServiceInterfaceMapper
    {
        public override IEnumerable<Type> ServiceInterfaceTypes => Mappings.Keys;


        private Dictionary<Type, Type> Mappings { get; }


		public OrleansServiceInterfaceMapper(IAssemblyProvider assemblyProvider)
		{
			Mappings = assemblyProvider.GetAllTypes()
				.Where(t => t.IsInterface && typeof(IGrain).IsAssignableFrom(t))
				.SelectMany(t => t.GetInterfaces().Where(i => i.GetCustomAttribute<HttpServiceAttribute>()!=null)
								  .Select(i => new { CallableInterface = t, ServiceInterface = i }))
				.ToDictionary(x => x.ServiceInterface, x => x.CallableInterface);

		    ExtractHealthStatusServiceType(Mappings.Values);
		}


	    public override Type GetGrainInterface(Type serviceInterface)
		{
			return Mappings[serviceInterface];
		}
	}
}