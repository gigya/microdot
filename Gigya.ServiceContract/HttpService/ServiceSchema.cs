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

        public string ResponseType { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public MethodSchema() { }

        public MethodSchema(MethodInfo info)
        {
            Name = info.Name;

            if (info.ReturnType.IsGenericType)
            {
                var resultType = info.ReturnType.GetGenericArguments().Single();
                IsRevocable = typeof(IRevocable).IsAssignableFrom(resultType);
                ResponseType = resultType.Name;
            }

            Parameters = info.GetParameters().Select(p => new ParameterSchema(p)).ToArray();
            Attributes = info.GetCustomAttributes().Select(a => new AttributeSchema(a)).ToArray();
        }
    }


    public class ParameterSchema
    {
        [JsonIgnore]
        public Type Type { get; set; }

        public string Name { get; set; }

        public string TypeName { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        public ParameterSchema() { }

        public ParameterSchema(ParameterInfo param)
        {
            Type = param.ParameterType;
            Name = param.Name;
            TypeName = param.ParameterType.AssemblyQualifiedName;
            Attributes = param.GetCustomAttributes().Select(_ => new AttributeSchema(_)).ToArray();
        }


        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            try
            {
                Type = Type.GetType(TypeName);
            }
            catch {}
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
