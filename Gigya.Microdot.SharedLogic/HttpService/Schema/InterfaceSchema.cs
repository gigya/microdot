using System;
using System.Linq;
using System.Reflection;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
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
}