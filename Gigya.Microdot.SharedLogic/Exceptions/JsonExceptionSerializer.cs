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
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
	/// <summary>
	/// Serializes and deserializes exceptions into JSON, with inheritance hierarchy tolerance.
	/// </summary>
	public class JsonExceptionSerializer
	{
		private readonly IJsonExceptionSerializationSettings _exceptionSerializationSettings;
		private IStackTraceEnhancer StackTraceEnhancer { get; }
        
        public JsonExceptionSerializer(IStackTraceEnhancer stackTraceEnhancer, IJsonExceptionSerializationSettings exceptionSerializationSettings)
        {
	        _exceptionSerializationSettings = exceptionSerializationSettings;
	        StackTraceEnhancer = stackTraceEnhancer;
        }

	    /// <summary>
	    /// Deserializes an exception from JSON, and uses the inheritance hierarchy embedded into the $type property in order to fall back to the
	    /// first type up the hierarchy that successfully deserializes. 
	    /// </summary>
	    /// <param name="json">The JSON to deserialize.</param>
	    /// <returns>The deserialized exception.</returns>
	    /// <exception cref="Newtonsoft.Json.JsonSerializationException">Thrown when the exception failed to deserialize.</exception>
	    public Exception Deserialize(string json)
	    {
	        var ex = JsonConvert.DeserializeObject<Exception>(json, _exceptionSerializationSettings.SerializerSettings);

	        if (ex == null)
	            throw new JsonSerializationException("Failed to deserialize exception.");

	        return ex;
	    }


        /// <summary>
        /// Serializes and exception into JSON, and embeds the entire inheritance hierarchy of the exception into the $type property.
        /// </summary>
        /// <param name="ex">The exception to serialize.</param>
        /// <returns>The JSON into which the exception was serialized.</returns>
        /// <exception cref="Newtonsoft.Json.JsonSerializationException">Thrown when the exception failed to serialize.</exception>
        public string Serialize(Exception ex)
        {
            var root = StackTraceEnhancer.ToJObjectWithBreadcrumb(ex);
            var current = root;

            while (current != null)
            {
                if (current.Property("StackTraceString") is JProperty stackTrace && stackTrace.Value.Type == JTokenType.String)
                    stackTrace.Value = StackTraceEnhancer.Clean(stackTrace.Value.Value<string>());

                if (current.Property("RemoteStackTraceString") is JProperty remoteStackTrace && remoteStackTrace.Value.Type != JTokenType.Null)
                    remoteStackTrace.Value = StackTraceEnhancer.Clean(remoteStackTrace.Value.Value<string>()) + "\r\n";

                current = current["InnerException"] is JObject inner ? inner : null;
            }

			return JsonConvert.SerializeObject(root, _exceptionSerializationSettings.SerializerSettings);
		}
    }
}
