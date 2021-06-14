using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gigya.Microdot.Interfaces.Configuration;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Security
{
    [ConfigurationRoot("Microdot.SerializationSecurity", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotSerializationSecurity : IConfigObject
    {
        public string DeserializationForbidenTypes = "System.Windows.Data.ObjectDataProvider,System.Diagnostics.Process";
    }

    public class ExcludeTypesSerializationBinder : ISerializationBinder
    {
        public readonly List<string> ExcludeTypes = new List<string>();

        public void ParseCommaSeparatedToExcludeTypes(string commaSeparated)
        {
            var items = commaSeparated?.Split(',');
            foreach (var item in items)
            {
                if (ExcludeTypes.Contains(item) == false)
                    ExcludeTypes.Add(item);
            }
        }

        public ExcludeTypesSerializationBinder()
        {
              
        }

        public bool IsExcluded(string typeName)
        {
            return ExcludeTypes.Any(s => typeName.ToLower(CultureInfo.InvariantCulture).Contains(s.ToLower(CultureInfo.InvariantCulture)));
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
                throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToName type '{serializedType.FullName}'");
            }

            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
}
