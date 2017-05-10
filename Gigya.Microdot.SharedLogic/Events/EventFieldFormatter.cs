using System;
using System.Globalization;

using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.Events
{


    /// <summary>
    /// Given an event field, serializes its value into a string as per our formatting preferences, depending on the
    /// type. Also, provides the type suffix, if required.
    /// </summary>
    internal static class EventFieldFormatter
    {

        public static string SerializeFieldValue(object fieldValue)
        {
            var tc = GetTypeCode(fieldValue);
            return SerializeFieldValue(tc, fieldValue);
        }


        public static void SerializeFieldValueAndTypeSuffix(object fieldValue, out string serializedValue, out string typeSuffix)
        {
            var tc = GetTypeCode(fieldValue);
            typeSuffix = GetFieldTypeSuffix(tc);
            serializedValue = SerializeFieldValue(tc, fieldValue);
        }


        static TypeCode GetTypeCode(object fieldValue)
        {
            if (fieldValue is Enum)
                return TypeCode.String;
            else
                return Type.GetTypeCode(fieldValue.GetType());
        }


        // See feature #38568: Support strongly-typed tags in Kibana
        static string GetFieldTypeSuffix(TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.Boolean:
                    return "_b";
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                    return "_i";
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return "_f";
                case TypeCode.DateTime:
                    return "_d";
                default:
                    return "";
            }
        }


        static string SerializeFieldValue(TypeCode tc, object fieldValue)
        {
            switch (tc)
            {
                case TypeCode.String:
                    return fieldValue.ToString();
                case TypeCode.Single:
                    return Math.Round((float)fieldValue, 4).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return Math.Round((double)fieldValue, 4).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return Math.Round((decimal)fieldValue, 4).ToString(CultureInfo.InvariantCulture);
                case TypeCode.DateTime:
                    return ((DateTime)fieldValue).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return fieldValue.ToString();
                case TypeCode.Object:
                    if (fieldValue is byte[])
                        return Convert.ToBase64String((byte[])fieldValue);
                    else
                        return SafeSerialize(fieldValue);
                default:
                    return SafeSerialize(fieldValue);
            }
        }


        private static string SafeSerialize(object value)
        {
            try
            {
                return JsonConvert.SerializeObject(value);
            }
            catch
            {
                return value.ToString();
            }
        }
    }

}
