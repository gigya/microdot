using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

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

        public AttributeSchema[] Attributes { get; set; }

        public MethodSchema() { }

        public MethodSchema(MethodInfo info)
        {
            Name = info.Name;
            Parameters = info.GetParameters().Select(_ => new ParameterSchema(_)).ToArray();
            Attributes = info.GetCustomAttributes().Select(_ => new AttributeSchema(_)).ToArray();
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
        public void OnDeserialized(StreamingContext context)
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
        public Attribute Attribute { get; set; } = null;

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
        public void OnDeserialized(StreamingContext context)
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
