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
			foreach (var type in new[] { typeof(JObject), typeof(JArray), typeof(JToken), typeof(JValue), typeof(JProperty), typeof(JConstructor) })
			{
				// Alternatively, can copy via JObject.ToString() and JObject.Parse() for true deep-copy.
				SerializationManager.Register(type, JTokenCloneCopier, ToStringSerializer, StandardJsonDeserializer);
			}
		}

		private static object JTokenCloneCopier(object original)
		{
		    var token = original as JToken;

		    if (token != null)
		        return token.DeepClone();

            return original;              
        }

		private static void ToStringSerializer(object untypedInput, BinaryTokenStreamWriter stream, Type expected = null)
        {
			SerializationManager.Serialize(untypedInput.ToString(), stream);
        }

		private static object StandardJsonDeserializer(Type expected, BinaryTokenStreamReader stream)
        {
            var str = SerializationManager.Deserialize<string>(stream);
            return JsonConvert.DeserializeObject(str, expected, JsonSettings);
        }
    }
}