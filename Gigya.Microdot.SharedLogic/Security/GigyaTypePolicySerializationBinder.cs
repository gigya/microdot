using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Security
{
    public interface IGigyaTypePolicySerializationBinder : ISerializationBinder
    {
        
    }
    
    public class GigyaTypePolicySerializationBinder : DefaultSerializationBinder, IGigyaTypePolicySerializationBinder
    {
        private readonly IMicrodotSerializationConstraints _serializationConstraints;
        
        public GigyaTypePolicySerializationBinder(IMicrodotSerializationConstraints serializationConstraints)
        {
            _serializationConstraints = serializationConstraints;
        }


        public override Type BindToType(string? assemblyName, string typeName)
        {
            _serializationConstraints.ThrowIfExcluded(typeName);
            
            var assemblyAndTypeName = _serializationConstraints. 
                TryGetAssemblyNameAndTypeReplacement(assemblyName, typeName);

            return base.BindToType(assemblyAndTypeName.AssemblyName, assemblyAndTypeName.TypeName);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            var serializedTypeFullName = serializedType.Assembly.FullName;
            
            _serializationConstraints.ThrowIfExcluded(serializedTypeFullName);
            
            base.BindToName(serializedType, out assemblyName, out typeName);

            var assemblyAndTypeName = _serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(
                serializedType, assemblyName, typeName);

            assemblyName = assemblyAndTypeName.AssemblyName;
            typeName = assemblyAndTypeName.TypeName;
        }
    }
}
