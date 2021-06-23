using System;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Security;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    public interface IJsonExceptionSerializationSettings
    {
        JsonSerializerSettings SerializerSettings { get; }

        JsonSerializer Serializer { get; }
    }

    public class JsonExceptionSerializationSettings : IJsonExceptionSerializationSettings
    {
        public JsonSerializerSettings SerializerSettings { get; }
        public JsonSerializer Serializer { get; }

        public JsonExceptionSerializationSettings(Func<MicrodotSerializationSecurityConfig> microdotSerializationSecurity, Func<ExceptionSerializationConfig> exceptionSerializationConfig, IExcludeTypesSerializationBinderFactory serializationBinderFactory)
        {
            SerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Binder = new ExceptionHierarchySerializationBinder(exceptionSerializationConfig, microdotSerializationSecurity, serializationBinderFactory),
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                Converters = {new StripHttpRequestExceptionConverter()}
            };

            Serializer = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.All,
                Binder = new ExceptionHierarchySerializationBinder(exceptionSerializationConfig, microdotSerializationSecurity, serializationBinderFactory),
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                Converters = {new StripHttpRequestExceptionConverter()}
            };
        }
    }

}