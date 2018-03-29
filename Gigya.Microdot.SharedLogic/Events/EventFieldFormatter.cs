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
                    return Math.Round((float)fieldValue, 3).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return Math.Round((double)fieldValue, 3).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return Math.Round((decimal)fieldValue, 3).ToString(CultureInfo.InvariantCulture);
                case TypeCode.DateTime:
                    return ((DateTime)fieldValue).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                case TypeCode.Boolean:
                    return fieldValue.ToString().ToLower();
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
