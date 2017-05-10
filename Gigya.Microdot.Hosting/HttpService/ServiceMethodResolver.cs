using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Gigya.Microdot.ServiceContract.Exceptions;
using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{
	internal class ServiceMethodResolver
	{
		private Dictionary<InvocationTarget, ServiceMethod> MethodCache { get; }

        public ServiceMethodResolver(IServiceInterfaceMapper mapper)
		{
            // get services methods 
            GrainMethods = mapper.ServiceInterfaceTypes
                                    .SelectMany(t => t.GetMethods() // get service's methods                                                       
                                    .Select(methodInfo => new ServiceMethod(mapper.GetGrainInterface(t), methodInfo))).ToArray();

            var incompatibleMethods = GrainMethods.Where(gm => gm.IsCompatible == false).ToArray();

			if (incompatibleMethods.Any())
			{
				var incompatibleMethodNames = string.Join("\n", incompatibleMethods.AsEnumerable());
				throw new ArgumentException("The specified assemblies contain service interfaces methods which have incompatible signatures:\n\n" + incompatibleMethodNames);
			}

            MethodCache = GrainMethods.ToDictionary(gm => new InvocationTarget(gm.ServiceInterfaceMethod));
		    SimpleMethodCache = GrainMethods.GroupBy(gm => gm.ServiceInterfaceMethod.Name).ToDictionary(a => a.Key, a => a.ToArray(), StringComparer.OrdinalIgnoreCase);
            TypedMethodCache = GrainMethods.GroupBy(gm => GetTypedMethodKey(gm.ServiceInterfaceMethod.DeclaringType, gm.ServiceInterfaceMethod.Name)).ToDictionary(a => a.Key, a => a.ToArray(), StringComparer.OrdinalIgnoreCase);
        }


	    private Dictionary<string, ServiceMethod[]> SimpleMethodCache { get; set; }
        private Dictionary<string, ServiceMethod[]> TypedMethodCache { get; set; }

        public ServiceMethod[] GrainMethods { get; } 

	    [Pure]
		public ServiceMethod Resolve(InvocationTarget target)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target), "An invocation target must be specified.");

            if (target.IsWeaklyTyped)
		    {
                ServiceMethod[] methods;
		        if (string.IsNullOrEmpty(target.TypeName))
		            SimpleMethodCache.TryGetValue(target.MethodName, out methods);
		        else
		            TypedMethodCache.TryGetValue(GetTypedMethodKey(target.TypeName, target.MethodName), out methods);

                if (methods == null)
                    throw new MissingMethodException("The specified request contains an unrecognized interface name, method name.");

                if (methods.Length > 1)
		        {
		            throw  new ProgrammaticException("Weakly-typed requests cannot be used for methods with more than one overload (including methods that are only differentiated by letter case)", unencrypted:new Tags {{"method", target.MethodName }, { "type", target.TypeName}});
		        }

                return methods.Single();
            }
          
            if (string.IsNullOrEmpty(target.TypeName) || string.IsNullOrEmpty(target.MethodName) || target.ParameterTypes == null)
				throw new ArgumentException("The specified invocation target is invalid.", nameof(target));

			ServiceMethod method;
			MethodCache.TryGetValue(target, out method);

			if (method == null)
				throw new MissingMethodException("The specified request contains an unrecognized interface name, method name or method overload.");

			return method;
		}


	    private string GetTypedMethodKey(Type type, string methodName)
	    {
	        return GetTypedMethodKey(type.FullName, methodName);
	    }
        private string GetTypedMethodKey(string type, string methodName)
	    {
	        return $"{type}|{methodName}";
	    }
	}
}