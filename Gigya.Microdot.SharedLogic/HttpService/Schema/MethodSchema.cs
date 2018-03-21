using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Attributes;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
    public class MethodSchema
    {
        public string Name { get; set; }

        public ParameterSchema[] Parameters { get; set; }

        public bool IsRevocable { get; set; }

        [Obsolete("Use Response.TypeName instead")]
        public string ResponseType { get; set; }

        public TypeSchema Response { get; set; }

        public AttributeSchema[] Attributes { get; set; }

        [JsonIgnore]
        public bool IsCached
        {
            get
            {
                if (_isCached == null)
                    _isCached = Attributes.Any(att => att.Attribute is CachedAttribute);

                return _isCached.Value;
            }
        }

        private bool? _isCached;


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
}