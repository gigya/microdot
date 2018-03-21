using System;
using System.Collections.Generic;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
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
}