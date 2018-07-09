#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;

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

            MethodCache = GrainMethods
                .SelectMany(gm => ExpandInvocationTargets(gm.ServiceInterfaceMethod), (method, target) => new { method, target })
                .ToDictionary(x => x.target, x => x.method);
			
		    SimpleMethodCache = GrainMethods.GroupBy(gm => gm.ServiceInterfaceMethod.Name).ToDictionary(a => a.Key, a => a.ToArray(), StringComparer.OrdinalIgnoreCase);
            TypedMethodCache = GrainMethods.GroupBy(gm => GetTypedMethodKey(gm.ServiceInterfaceMethod.DeclaringType, gm.ServiceInterfaceMethod.Name)).ToDictionary(a => a.Key, a => a.ToArray(), StringComparer.OrdinalIgnoreCase);
        }

		private static IEnumerable<InvocationTarget> ExpandInvocationTargets(MethodInfo methodInfo)
		{
            var parameters = methodInfo.GetParameters();
			
			yield return new InvocationTarget(methodInfo, parameters.ToArray());

		    for (int i = parameters.Length - 1; i > -1; i--)
		    {
		        if (parameters[i].IsOptional)
		            yield return new InvocationTarget(methodInfo, parameters.Take(i).ToArray());
		        else
		            yield break;
		    }
        }


		private Dictionary<string, ServiceMethod[]> SimpleMethodCache { get;  }
        private Dictionary<string, ServiceMethod[]> TypedMethodCache { get; }

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
					throw new ProgrammaticException("Weakly-typed requests cannot be used for methods with more than one overload (including methods that are only differentiated by letter case)",
						unencrypted: new Tags {{"method", target.MethodName}, {"type", target.TypeName}});
				}

				return methods.Single();
			}
			else
			{
				if (string.IsNullOrEmpty(target.TypeName) || string.IsNullOrEmpty(target.MethodName) || target.ParameterTypes == null)
					throw new ArgumentException("The specified invocation target is invalid.", nameof(target));

				MethodCache.TryGetValue(target, out ServiceMethod method);

				if (method == null)
					throw new MissingMethodException("The specified request contains an unrecognized interface name, method name or method overload.");

				return method;
			}
		}


	    private static string GetTypedMethodKey(Type type, string methodName)
	    {
	        return GetTypedMethodKey(type.FullName, methodName);
	    }

        private static string GetTypedMethodKey(string type, string methodName)
	    {
	        return $"{type}|{methodName}";
	    }
	}
}