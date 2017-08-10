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
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Common.Contracts.HttpService
{

    /// <summary>
    /// Contains a collection of interfaces, methods and method parameters, along with their attributes. Parameter types
    /// and attributes are both weakly and strongly deserialized, so clients can convenienetly work with real objects if
    /// they reference the needed assemblies, or work against strings/JObjects if they don't.
    /// </summary>
    public class ServiceSchema
    {
        public InterfaceSchema[] Interfaces { get; set; }

        public ServiceSchema() {}

        public ServiceSchema(Type[] interfaces)
        {
            Interfaces = interfaces.Select(_ => new InterfaceSchema(_)).ToArray();
        }

    }


    public class InterfaceSchema
    {
        public string Name { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public MethodSchema[] Methods { get; set; }

        public InterfaceSchema() {}

        public InterfaceSchema(Type iface)
        {
            if (!iface.IsInterface)
                throw new ArgumentException("Not an itnerface");
            Name = iface.FullName;
            Attributes = iface.GetCustomAttributes().Select(_ => new AttributeSchema(_)).ToArray();
            Methods = iface.GetMethods().Select(_ => new MethodSchema(_)).ToArray();
        }
    }

    public class MethodSchema
    {
        public string Name { get; set; }

        public ParameterSchema[] Parameters { get; set; }

        public bool IsRevocable { get; set; }

        public TypeSchema ResponseType { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public MethodSchema() { }

        public MethodSchema(MethodInfo info)
        {
            Name = info.Name;

            if (info.ReturnType.IsGenericType)
            {
                var resultType = info.ReturnType.GetGenericArguments().Single();
                IsRevocable = typeof(IRevocable).IsAssignableFrom(resultType);
                ResponseType = new TypeSchema(resultType, info.ReturnType.GetCustomAttributes());
            }
            else
            {
                ResponseType = new TypeSchema(info.ReturnType, info.ReturnType.GetCustomAttributes());
            }

            Parameters = info.GetParameters().Select(p => new ParameterSchema(p)).ToArray();
            Attributes = info.GetCustomAttributes().Select(a => new AttributeSchema(a)).ToArray();
        }
    }


    public class TypeSchema
    {
        [JsonIgnore]
        public Type Type { get; set; }

        public string TypeName { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public FieldSchema[] Fields { get; set; }

        public TypeSchema() { }

        public TypeSchema(Type type, IEnumerable<Attribute> attributes)
        {
            Type = type;
            TypeName = type.AssemblyQualifiedName;
            Attributes = attributes.Select(_ => new AttributeSchema(_)).ToArray();

            if (IsCompositeType(type))
                Fields = GetFields().ToArray();
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

        protected virtual IEnumerable<FieldSchema> GetFields()
        {
            return GetFields(Type);
        }

        private IEnumerable<FieldSchema> GetFields(Type type)
        {
            var baseFields = type.BaseType != typeof(object) && type.BaseType != null ? GetFields(type.BaseType) : new FieldSchema[0];
            var properties = type.GetProperties().Select(_ => new FieldSchema(_));
            var fields = type.GetFields().Select(_ => new FieldSchema(_));
            return baseFields.Concat(properties).Concat(fields);
        }

        private bool IsCompositeType(Type type)
        {
            return !type.IsValueType && !(type == typeof(string));
        }
    }

    public class ParameterSchema: TypeSchema
    {
        public string Name { get; set; }

        public ParameterSchema() { }

        public ParameterSchema(ParameterInfo param): this (param.Name, param.ParameterType, param.GetCustomAttributes())
        {
        }

        protected ParameterSchema(string name, Type type, IEnumerable<Attribute> attributes): base(type, attributes)
        {
            Name = name;
        }
    }

    public class FieldSchema : ParameterSchema
    {
        public FieldSchema() { }

        public FieldSchema(FieldInfo field) : base(field.Name, field.FieldType, field.GetCustomAttributes())
        {
        }

        public FieldSchema(PropertyInfo property) : base(property.Name, property.PropertyType, property.GetCustomAttributes())
        {
        }

        protected override IEnumerable<FieldSchema> GetFields()
        {
            // A field should not contain inner fields. Prevent infinite loop
            return new FieldSchema[0];
        }
    }


    public class AttributeSchema
    {
        [JsonIgnore]
        public Attribute Attribute { get; set; }

        public string TypeName { get; set; }

        public JObject Data { get; set; }


        public AttributeSchema() {}

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
    }

}
