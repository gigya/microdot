using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Security
{
    public class ExcludeTypesSerializationBinder : ISerializationBinder
    {
        public readonly List<string> ExcludeTypes = new List<string>();
        public Type BindToType(string? assemblyName, string typeName)
        {
            if (ExcludeTypes.Any(s => typeName.ToLower(CultureInfo.InvariantCulture).Contains(s.ToLower(CultureInfo.InvariantCulture))))
            {
                throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToType type '{typeName}'");
            }

            return new DefaultSerializationBinder().BindToType(assemblyName, typeName);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            var name = serializedType.Assembly.FullName;
            if (ExcludeTypes.Any(s => name.ToLower(CultureInfo.InvariantCulture).Contains(s.ToLower(CultureInfo.InvariantCulture))))
                
            {
                throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToName type '{serializedType.FullName}'");
            }

            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
}
