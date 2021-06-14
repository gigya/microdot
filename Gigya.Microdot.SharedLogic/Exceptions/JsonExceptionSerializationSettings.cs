using System;
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

        private readonly Func<ExceptionSerializationConfig> _exceptionSerializationConfig;
        private readonly Func<Security.MicrodotSerializationSecurity> _microdotSerializationSecurity;
        public JsonSerializerSettings SerializerSettings { get; }
        public JsonSerializer Serializer { get; }

        public JsonExceptionSerializationSettings(Func<Security.MicrodotSerializationSecurity> microdotSerializationSecurity, Func<ExceptionSerializationConfig> exceptionSerializationConfig)
        {
            _exceptionSerializationConfig = exceptionSerializationConfig;
            _microdotSerializationSecurity = microdotSerializationSecurity;

            SerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Binder = new ExceptionHierarchySerializationBinder(_exceptionSerializationConfig, _microdotSerializationSecurity),
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                Converters = {new StripHttpRequestExceptionConverter()}
            };

            Serializer = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.All,
                Binder = new ExceptionHierarchySerializationBinder(_exceptionSerializationConfig, _microdotSerializationSecurity),
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                Converters = {new StripHttpRequestExceptionConverter()}
            };
        }
    }

}