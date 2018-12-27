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
using System.Linq;
using System.Text.RegularExpressions;
using Gigya.ServiceContract.Exceptions;
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

        private const string ParamCaptureName = "param";
        private static readonly Regex ParamRegex = new Regex(@"Path\s'(?<" + ParamCaptureName + ">.*)'.$", RegexOptions.Compiled | RegexOptions.CultureInvariant);


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

            try
            {
                if (value is string && Type.GetTypeCode(paramType) == TypeCode.Object &&
                    paramType != typeof(DateTimeOffset) && paramType != typeof(TimeSpan) && paramType != typeof(Guid) &&
                    paramType != typeof(byte[]))
                    return JsonConvert.DeserializeObject((string) value, paramType);

                return JToken.FromObject(value).ToObject(targetType, Serializer);
            }
            catch (JsonReaderException jsException)
            {
                var parameterPath = string.IsNullOrEmpty(jsException.Path) ? new string[0] : jsException.Path.Split('.');
                throw new InvalidParameterValueException(null, parameterPath, jsException.Message, innerException: jsException);
            }
            catch (JsonSerializationException serException)
            {
                string parameterPathStr = null;
                var match = ParamRegex.Match(serException.Message);
                if (match.Success)
                    parameterPathStr = match.Groups[ParamCaptureName]?.Value;

                throw new InvalidParameterValueException(null, parameterPathStr?.Split('.') ?? new string[0], serException.Message, innerException: serException);
            }
            catch (Exception ex)
            {
                throw new InvalidParameterValueException(null, null, ex.Message, innerException: ex);
            }

        }
    }
}
