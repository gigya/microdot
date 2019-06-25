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

#endregion Copyright

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.Serialization;
using System;
using System.Linq;
using System.Collections.Generic;

// ReSharper disable AssignNullToNotNullAttribute

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>
    /// This class is called by the Orleans runtime to perform serialization for special types, and should not be called directly from your code.
    /// </summary>
    public class OrleansCustomSerialization : IExternalSerializer
    {
        protected readonly Dictionary<string,Type> _supportedTypes;

        public Func<JsonSerializerSettings> JsonSettingsFunc { get; set; }

        public OrleansCustomSerialization()
        {
            _supportedTypes = new[]
            {
                typeof(JObject), 
                typeof(JArray), 
                typeof(JToken), 
                typeof(JValue), 
                typeof(JProperty), 
                typeof(JConstructor)
            }.ToDictionary(t => t.FullName);

            JsonSettingsFunc = () => new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.None
            };
        }

        public virtual bool IsSupportedType(Type itemType)
        {
            if(itemType == null)
                throw new ArgumentNullException(nameof(itemType) + " can not be null");

            var result = _supportedTypes.ContainsKey(itemType.FullName);

            return result;
        }

        public virtual object DeepCopy(object source, ICopyContext context)
        {
            if (source is JToken token)
                return token.DeepClone();

            return source;
        }

        public virtual void Serialize(object item, ISerializationContext context, Type expectedType)
        {
            var serializedObject = JsonConvert.SerializeObject(item, expectedType, JsonSettingsFunc());
            SerializationManager.SerializeInner(serializedObject, context);
        }

        public virtual object Deserialize(Type expectedType, IDeserializationContext context)
        {
            var str = SerializationManager.DeserializeInner<string>(context);
            return JsonConvert.DeserializeObject(str, expectedType, JsonSettingsFunc());
        }

        public virtual void RegisterType(Type itemType)
        {
            if(itemType == null)
                throw new ArgumentNullException(nameof(itemType) + " can not be null");
            
            _supportedTypes[itemType.FullName] = itemType;
        }

        public virtual void UnRegisterType(Type itemType)
        {
            if(itemType == null)
                throw new ArgumentNullException(nameof(itemType) + " can not be null");

            _supportedTypes.Remove(itemType.FullName);
        }

        public virtual IEnumerable<Type> GetRegistered()
        {
            return _supportedTypes.Values;
        }
    }
}