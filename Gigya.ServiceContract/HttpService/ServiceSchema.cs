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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Common.Contracts.HttpService
{

    /// <summary>
    /// Contains a collection of Binterfaces, methods and method parameters, along with their attributes. Parameter types
    /// and attributes are both weakly and strongly deserialized, so clients can convenienetly work with real objects if
    /// they reference the needed assemblies, or work against strings/JObjects if they don't.
    /// </summary>
    public class ServiceSchema
    {
        public InterfaceSchema[] Interfaces { get; set; }

        public ServiceSchema() { }

        public ServiceSchema(Type[] interfaces)
        {
            Interfaces = interfaces.Select(_ => new InterfaceSchema(_)).ToArray();
            SetHashCode();
        }

        public string Hash { get; set; }

        private void SetHashCode()
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            using (SHA1 sha = new SHA1CryptoServiceProvider())
            {
                JsonSerializer.Create().Serialize(writer, this);
                stream.Seek(0, SeekOrigin.Begin);
                Hash = Convert.ToBase64String(sha.ComputeHash(stream));
            }
        }

    }

    public class InterfaceSchema
    {
        public string Name { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public MethodSchema[] Methods { get; set; }

        public InterfaceSchema() { }

        public InterfaceSchema(Type iface)
        {
            if (!iface.IsInterface)
                throw new ArgumentException("Not an interface");

            Name = iface.FullName;
            Methods = iface.GetMethods().Select(m => new MethodSchema(m)).ToArray();
            Attributes = iface
                .GetCustomAttributes()
                .Where(AttributeSchema.FilterAttributes)
                .Select(a => new AttributeSchema(a))
                .ToArray();
        }
    }

    public class MethodSchema
    {
        public string Name { get; set; }

        public ParameterSchema[] Parameters { get; set; }

        public bool IsRevocable { get; set; }

        [Obsolete("Use Response.TypeName instead")]
        public string ResponseType { get; set; }

        public TypeSchema Response { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public MethodSchema() { }

        public MethodSchema(MethodInfo info)
        {
            Name = info.Name;


            if (info.ReturnType == typeof(Task))
            {
                Response = null;

            }
            else if (info.ReturnType.IsGenericType && info.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = info.ReturnType.GetGenericArguments().Single();
                if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Revocable<>))
                {
                    IsRevocable = true;
                    resultType = resultType.GetGenericArguments().Single();
                }
                Response = new TypeSchema(resultType, info.ReturnType.GetCustomAttributes());
            }
            else
            {
                Response = new TypeSchema(info.ReturnType, info.ReturnType.GetCustomAttributes());
            }

            ResponseType = Response?.TypeName;
            Parameters = info.GetParameters().Select(p => new ParameterSchema(p)).ToArray();
            Attributes = info
                .GetCustomAttributes()
                .Where(AttributeSchema.FilterAttributes)
                .Select(a => new AttributeSchema(a))
                .ToArray();
        }
    }

    public class SimpleTypeSchema
    {
        [JsonIgnore]
        public Type Type { get; set; }

        public string TypeName { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public SimpleTypeSchema() { }

        public SimpleTypeSchema(Type type, IEnumerable<Attribute> attributes)
        {
            Type = type;
            TypeName = type.AssemblyQualifiedName;
            Attributes = attributes
                    .Where(AttributeSchema.FilterAttributes)
                    .Select(a => new AttributeSchema(a))
                    .ToArray();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            try
            {
                Type = Type.GetType(TypeName);
            }
            catch { }
        }
    }

    public class TypeSchema : SimpleTypeSchema
    {
        public FieldSchema[] Fields { get; set; }

        public TypeSchema() { }

        public TypeSchema(Type type, IEnumerable<Attribute> attributes) : base(type, attributes)
        {
            if (IsCompositeType(type))
                Fields = GetFields(type).ToArray();

        }

        private IEnumerable<FieldSchema> GetFields(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(_ => new FieldSchema(_));
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(_ => new FieldSchema(_));
            return properties.Concat(fields);
        }

        private bool IsCompositeType(Type type)
        {

            return !type.IsValueType && !(type == typeof(string));
        }
    }

    public class ParameterSchema : TypeSchema
    {
        public string Name { get; set; }

        public ParameterSchema() { }

        public ParameterSchema(ParameterInfo param) : this(param.Name, param.ParameterType, param.GetCustomAttributes())
        {
        }

        protected ParameterSchema(string name, Type type, IEnumerable<Attribute> attributes) : base(type, attributes)
        {
            Name = name;
        }
    }

    public class FieldSchema : SimpleTypeSchema
    {
        public string Name { get; set; }

        public FieldSchema() { }

        public FieldSchema(FieldInfo field) : base(field.FieldType, field.GetCustomAttributes())
        {
            Name = field.Name;
        }

        public FieldSchema(PropertyInfo property) : base(property.PropertyType, property.GetCustomAttributes())
        {
            Name = property.Name;
        }
    }


    public class AttributeSchema
    {
        [JsonIgnore]
        public Attribute Attribute { get; set; }

        public string TypeName { get; set; }

        public JObject Data { get; set; }

        public AttributeSchema() { }

        public AttributeSchema(Attribute attribute)
        {
            Attribute = attribute;
            TypeName = attribute.GetType().AssemblyQualifiedName;
            Data = JObject.FromObject(attribute);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            try
            {
                Type t = Type.GetType(TypeName);

                if (t != null)
                    Attribute = (Attribute)Data.ToObject(t);
            }
            catch { }
        }

        internal static bool FilterAttributes(Attribute a)
        {
            return a.GetType().Namespace?.StartsWith("System.Diagnostics") == false && a.GetType().Namespace?.StartsWith("System.Security") == false;
        }
    }

}
