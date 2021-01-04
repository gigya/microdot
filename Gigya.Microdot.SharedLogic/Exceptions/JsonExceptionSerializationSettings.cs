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
        public JsonSerializerSettings SerializerSettings { get; }
        public JsonSerializer Serializer { get; }

        public JsonExceptionSerializationSettings(Func<ExceptionSerializationConfig> exceptionSerializationConfig)
        {
            _exceptionSerializationConfig = exceptionSerializationConfig;

            SerializerSettings = new JsonSerializerSettings
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
    }

}