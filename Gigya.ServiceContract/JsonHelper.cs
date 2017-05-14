using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Common.Contracts
{
    /// <summary>
    /// Utility class for converting between JSON and .NET values.
    /// </summary>
    public static class JsonHelper
    {
        private static JsonSerializer Serializer { get; } = new JsonSerializer { DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind };


        /// <summary>
        /// Converts values that were deserialized from JSON with weak typing (e.g. into <see cref="object"/>) back into
        /// their strong type, according to the specified target type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The type the value should be converted into.</param>
        /// <returns></returns>
        public static object ConvertWeaklyTypedValue(object value, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (value == null || targetType.IsInstanceOfType(value))
                return value;

            var paramType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (paramType == typeof(DateTime) && value is DateTimeOffset)
            {
                var dto = (DateTimeOffset)value;

                if (dto.Offset == TimeSpan.Zero)
                    return dto.UtcDateTime;
                else
                    return dto.LocalDateTime;
            }

            if (value is string && Type.GetTypeCode(paramType) == TypeCode.Object && paramType != typeof(DateTimeOffset) && paramType != typeof(TimeSpan) && paramType != typeof(Guid))
                return JsonConvert.DeserializeObject((string)value, paramType);

            return JToken.FromObject(value).ToObject(targetType, Serializer);
        }
    }
}
