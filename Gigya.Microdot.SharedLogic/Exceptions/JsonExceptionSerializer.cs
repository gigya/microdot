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
using System.Linq;
using System.Net.Http;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
	/// <summary>
	/// Serializes and deserializes exceptions into JSON, with inheritance hiearchy tolerance.
	/// </summary>
	public static class JsonExceptionSerializer
	{
		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.All,
			Binder = new ExceptionHierarchySerializationBinder(),
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            Converters = { new StripHttpRequestExceptionConverter() }
        };

		/// <summary>
		/// Serializes and exception into JSON, and embeds the entire inheritance hiearchy of the exception into the $type property.
		/// </summary>
		/// <param name="ex">The exception to serialize.</param>
		/// <returns>The JSON into which the exception was serialized.</returns>
		/// <exception cref="Newtonsoft.Json.JsonSerializationException">Thrown when the exception failed to serialize.</exception>
		public static string Serialize(Exception ex)
		{
			return JsonConvert.SerializeObject(ex, typeof(Exception), Settings);
		}

		/// <summary>
		/// Deserializes an exception from JSON, and uses the inheritance hiearchy embedded into the $type property in order to fall back to the
		/// first type up the hiearchy that successfully deserializes. 
		/// </summary>
		/// <param name="json">The JSON to deserialize.</param>
		/// <returns>The deserialized exception.</returns>
		/// <exception cref="Newtonsoft.Json.JsonSerializationException">Thrown when the exception failed to deserialize.</exception>
		public static Exception Deserialize(string json)
		{
			var ex = JsonConvert.DeserializeObject<Exception>(json, Settings);

			if (ex == null)
				throw new JsonSerializationException("Failed to deserialize exception.");

			return ex;
		}
	}

	internal class ExceptionHierarchySerializationBinder : DefaultSerializationBinder
	{
		public override Type BindToType(string assemblyName, string typeName)
		{
			var assemblyNames = assemblyName.Split(':');
			var typeNames = typeName.Split(':');
			var type = assemblyNames.Zip(typeNames, TryBindToType).FirstOrDefault(t => t != null);

			return type ?? base.BindToType(typeof(Exception).Assembly.GetName().Name, typeof(Exception).FullName);
		}

		private Type TryBindToType(string assemblyName, string typeName)
		{
			try
			{
				return base.BindToType(assemblyName, typeName);
			}
			catch (JsonSerializationException)
			{
				return null;
			}
		}

		public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
		{
			if (serializedType.IsSubclassOf(typeof(Exception)))
			{
				var inheritanceHierarchy = GetInheritanceHierarchy(serializedType)
					.Where(t => t.IsAbstract == false && t != typeof(object) && t != typeof(Exception))
					.ToArray();

				typeName = string.Join(":", inheritanceHierarchy.Select(t => t.FullName));
				assemblyName = string.Join(":", inheritanceHierarchy.Select(t => t.Assembly.GetName().Name));
			}
			else
			{
				base.BindToName(serializedType, out assemblyName, out typeName);
			}
		}

        private static IEnumerable<Type> GetInheritanceHierarchy(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                yield return current;
        }
    }



    internal class StripHttpRequestExceptionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(HttpRequestException);
        }


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var httpException = (HttpRequestException)value;

            var innerException = httpException?.InnerException ??
                new EnvironmentException("[HttpRequestException] " + httpException?.RawMessage(),
                unencrypted: new Tags { { "originalStackTrace", httpException?.StackTrace } });
            
            JObject.FromObject(innerException, serializer).WriteTo(writer);
        }


        public override bool CanRead => false;
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
