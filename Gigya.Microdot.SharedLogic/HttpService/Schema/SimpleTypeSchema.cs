using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
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
}