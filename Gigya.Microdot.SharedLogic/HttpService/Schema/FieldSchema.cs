using System.Reflection;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
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
}