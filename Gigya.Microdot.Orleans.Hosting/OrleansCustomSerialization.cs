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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.CodeGeneration;
using Orleans.Serialization;

namespace Gigya.Microdot.Orleans.Hosting
{
	/// <summary>
	/// This class is called by the Orleans runtime to perform serialization for special types, and should not be called directly from your code.
	/// </summary>
	[RegisterSerializer]
	// ReSharper disable once UnusedMember.Global
	public class OrleansCustomSerialization
	{
		private static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.Auto,
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.Indented,
			DateParseHandling = DateParseHandling.None
		};

		/// <summary>
		/// This method is called by the Orleans runtime (because of the RegisterSerializerAttribute) to register serializers for special types, 
		/// and should not be called directly from your code.
		/// </summary>
		// ReSharper disable once UnusedMember.Global
		public static void Register()
		{
			foreach(var type in new[] {typeof(JObject), typeof(JArray), typeof(JToken), typeof(JValue), typeof(JProperty), typeof(JConstructor)})
			{
				// Alternatively, can copy via JObject.ToString() and JObject.Parse() for true deep-copy.
				SerializationManager.Register(type, JTokenCloneCopier, ToStringSerializer, StandardJsonDeserializer);
			}
		}

		private static object JTokenCloneCopier(object original)
		{
			if (original is JToken token)
				return token.DeepClone();

			return original;
		}

		private static void ToStringSerializer(object untypedInput, BinaryTokenStreamWriter stream, Type expected = null) { SerializationManager.Serialize(untypedInput.ToString(), stream); }

		private static object StandardJsonDeserializer(Type expected, BinaryTokenStreamReader stream)
		{
			var str = SerializationManager.Deserialize<string>(stream);
			return JsonConvert.DeserializeObject(str, expected, JsonSettings);
		}
	}
}