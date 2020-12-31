using System;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    public interface IJsonExceptionSerializationSettings
    {
        JsonSerializerSettings GetExceptionSerializerSettings();

        JsonSerializer GetSerializer();
    }
    public class JsonExceptionSerializationSettings:IJsonExceptionSerializationSettings
    {
        private readonly Func<ExceptionSerializationConfig> _exceptionSerializationConfig;

        private JsonSerializerSettings Settings;

        private JsonSerializer Serializer;

        public JsonExceptionSerializationSettings(Func<ExceptionSerializationConfig> exceptionSerializationConfig)
        {
            _exceptionSerializationConfig = exceptionSerializationConfig;

            Settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Binder = new ExceptionHierarchySerializationBinder(_exceptionSerializationConfig),
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                Converters = {new StripHttpRequestExceptionConverter()}
            };

            Serializer = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.All,
                Binder = new ExceptionHierarchySerializationBinder(_exceptionSerializationConfig),
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                Converters = {new StripHttpRequestExceptionConverter()}
            };
        }

        public JsonSerializerSettings GetExceptionSerializerSettings()
        {
            return Settings;
        }

        public JsonSerializer GetSerializer()
        {
            return Serializer;
        }
    }
}