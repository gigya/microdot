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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.Runtime;
using Orleans.Serialization;

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>
    /// This class is called by the Orleans runtime to perform serialization for special types, and should not be called directly from your code.
    /// </summary>
    public class OrleansCustomSerialization : IExternalSerializer
    {
        private Logger _logger;
        private readonly Type[] _supportedTypes;

        public OrleansCustomSerialization()
        {
            _supportedTypes = new[]
            {
                typeof(JObject), typeof(JArray), typeof(JToken), typeof(JValue), typeof(JProperty), typeof(JConstructor)
            };
        }

        private static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.None
        };

        public void Initialize(Logger logger)
        {
            _logger = logger;
        }

        public bool IsSupportedType(Type itemType) => _supportedTypes.Any(type => type == itemType);

        public object DeepCopy(object source, ICopyContext context)
        {
            if (source is JToken token)
                return token.DeepClone();

            return source;
        }

        public void Serialize(object item, ISerializationContext context, Type expectedType)
        {
            SerializationManager.SerializeInner(item.ToString(), context, item.GetType());
        }

        public object Deserialize(Type expectedType, IDeserializationContext context)
        {
            var str = (string)SerializationManager.DeserializeInner(expectedType.GetType(), context);

            return JsonConvert.DeserializeObject(str, expectedType, JsonSettings);
        }
    }
}