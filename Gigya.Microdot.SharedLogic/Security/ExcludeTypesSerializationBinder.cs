using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Security
{
    public class ExcludeTypesSerializationBinder : ISerializationBinder
    {
        private readonly HashSet<string> _excludeTypes = new HashSet<string>();

        public void ParseCommaSeparatedToExcludeTypes(string commaSeparated)
        {
            var items = commaSeparated?.Split(',') ?? new string[0];
            foreach (var item in items) 
                _excludeTypes.Add(item);
        }

       
        public bool IsExcluded(string typeName)
        {
            return _excludeTypes.Any(s => typeName.ToLower(CultureInfo.InvariantCulture).Contains(s.ToLower(CultureInfo.InvariantCulture)));
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (IsExcluded(typeName))
            {
                throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToType type '{typeName}'");
            }

            return new DefaultSerializationBinder().BindToType(assemblyName, typeName);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            var name = serializedType.Assembly.FullName;
            if (IsExcluded(name))
            {
                throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToName type '{name}'");
            }

            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
}
