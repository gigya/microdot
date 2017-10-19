using System;
using System.Net.Http;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
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