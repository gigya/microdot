using System;
using System.Collections.Generic;
using System.Reflection;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
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
}