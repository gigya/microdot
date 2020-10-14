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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gigya.Microdot.SharedLogic.Events;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.HttpService
{
    /// <remarks>If you add anything here, note that derived classes are sometimes cloned; new fields should be cloned too.</remarks>
    [Serializable]
    public abstract class ExtendableJson
    {
        /// <summary>
        /// This property de/serializes automatically
        /// See <see cref="https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_JsonExtensionDataAttribute.htm"/>
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> AdditionalProperties;
    }

	public class HttpServiceRequest : ExtendableJson
    {
        public const string ProtocolVersion = "1";
        
        [JsonProperty(Order = 0)]
		public OrderedDictionary Arguments { get; set; }

        [JsonProperty(Order = 1)]
        public TracingData TracingData { get; set; }

        [JsonProperty(Order = 2)]
        public RequestOverrides Overrides { get; set; }

        [JsonProperty(Order = 3)]
		public InvocationTarget Target { get; set; }
        
        /// <summary>
        /// Constructor for deserialization. Should not set any property values.
        /// </summary>
        public HttpServiceRequest() { }

        private HttpServiceRequest(ICollection arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

			Arguments = new OrderedDictionary(arguments.Count);
        }

        /// <summary>
        /// Used for weakly-typed requests. Arguments sent in this way are matched by name only, the order isn't used.
        /// </summary>
        /// <param name="targetMethod">The name of the method to be called.</param>
        /// <param name="typeName">The type on which to call the method.</param>
        /// <param name="arguments">A dictionary of arguments to be supplied to the method, where the key is the name of the argument.</param>
        public HttpServiceRequest(string targetMethod, string typeName, Dictionary<string, object> arguments) : this(arguments)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));

            Target = new InvocationTarget(targetMethod, typeName);

            foreach(var argument in arguments)
                Arguments.Add(argument.Key, argument.Value);
        }

        /// <summary>
        /// Used for strongly-typed requests. Arguments sent in this way are matched by order only, the name isn't used.
        /// </summary>
        /// <param name="targetMethod">The <see cref="MethodInfo"/> of the method to be called.</param>
        /// <param name="arguments">An array of arguments to be supplied to the method, where the position of the elements match the position of the arguments.</param>
        public HttpServiceRequest(MethodInfo targetMethod, object[] arguments) : this(arguments)
		{
			if (targetMethod == null)
				throw new ArgumentNullException(nameof(targetMethod));
		    
			var parameters = targetMethod.GetParameters();
			
			if (arguments.Length < parameters.Count(a => a.IsOptional == false))
				throw new ArgumentException("An incorrect number of arguments was supplied for the specified target method.", nameof(arguments));

			Target = new InvocationTarget(targetMethod, parameters);

            
            for (int i = 0; i < arguments.Length; i++)
				Arguments.Add(parameters[i].Name, arguments[i]);
		}
        
        public HttpServiceRequest(string targetMethod, string typeName, Dictionary<string, object> arguments, IEnumerable<Type> types) : this(arguments)
        {
            if (targetMethod == null)
            throw new ArgumentNullException(nameof(targetMethod));

            Target = new InvocationTarget(targetMethod, typeName, types);

                foreach (var argument in arguments)
            Arguments.Add(argument.Key, argument.Value);
        }
    }

    public class InvocationTarget : ExtendableJson
    {
		public string TypeName { get; set; }
		public string MethodName { get; set; }
        
        /// <summary>
        /// Calling a special service endpoint, like "config", "schema".
        /// </summary>
        public string Endpoint { get; set; }

		public string[] ParameterTypes { get; set; }

        [JsonIgnore]
        public bool IsWeaklyTyped => MethodName != null && ParameterTypes == null;

		public InvocationTarget() { }

        public InvocationTarget(string methodName, string typeName = null)
	    {
	        MethodName = methodName;
            TypeName = typeName;
	    }

		public InvocationTarget(MethodInfo method, ParameterInfo[] parameterTypes)
		{
			TypeName = method.DeclaringType.FullName;
			MethodName = method.Name;
			ParameterTypes = parameterTypes.Select(p => GetCleanTypeName(p.ParameterType)).ToArray();
		}
        public InvocationTarget(string methodName, string typeName, IEnumerable<Type> parameterTypes)
        {
            TypeName = typeName;
            MethodName = methodName;
            ParameterTypes = parameterTypes.Select(GetCleanTypeName).ToArray();
        }

        private static string GetCleanTypeName(Type type)
		{            
			return Regex.Replace(type.ToString(), @"`\d", "")
				.Replace("System.Byte", "byte")
				.Replace("System.SByte", "sbyte")
				.Replace("System.Int16", "short")
				.Replace("System.UInt16", "ushort")
				.Replace("System.Int32", "int")
				.Replace("System.UInt32", "uint")
				.Replace("System.Int64", "long")
				.Replace("System.UInt64", "ulong")
				.Replace("System.Float", "float")
				.Replace("System.Single", "decimal")
				.Replace("System.Double", "double")
				.Replace("System.Char", "char")
				.Replace("System.Boolean", "bool")
				.Replace("System.String", "string")
				.Replace("System.Object", "object")
				.Replace("System.Collections.Generic.", "");
		}


		public override string ToString()
		{
		    if (IsWeaklyTyped)
		        return $"{TypeName}.{MethodName}";

            return $"{TypeName}.{MethodName}({string.Join(",", ParameterTypes)})";
		}


		public override bool Equals(object obj)
		{
			var other = obj as InvocationTarget;
			return other != null && TypeName == other.TypeName && MethodName == other.MethodName && ParameterTypes.SequenceEqual(other.ParameterTypes);
		}


		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = TypeName?.GetHashCode() ?? 0;
				hashCode = (hashCode * 397) ^ (MethodName?.GetHashCode() ?? 0);
				hashCode = (hashCode * 397) ^ (ParameterTypes?.Length.GetHashCode() ?? 0);
				return hashCode;
			}
		}

	}

    public class TracingData : ExtendableJson
    {
        /// <summary>This is the root request ID that's passed to all grains and services in the request lifetime. It is
        /// used to tie together all events and log messages issued during the processing of a request, across all
        /// machines involved.</summary>
        [JsonProperty]
        public string RequestID { get; set; }

        [JsonProperty]
        public string HostName { get; set; }

        [JsonProperty]
        public string ServiceName { get; set; }

        [JsonProperty]
        public string SpanID { get; set; }

        [JsonProperty]
        public string ParentSpanID { get; set; }

        /// <summary>
        /// The time at which the request was sent from the client.
        /// </summary>
        [JsonProperty]
        public DateTimeOffset? SpanStartTime { get; set; }

        /// <summary>
        /// The time at which the topmost API gateway is going to give up on the whole end-to-end request, after which
        /// it makes no sense to try and handle it, or to subsequently call other services.
        /// </summary>
        [JsonProperty]
        public DateTimeOffset? AbandonRequestBy { get; set; }

        [JsonProperty]
        public Dictionary<string, ContextTag> Tags { get; set; }
    }
}