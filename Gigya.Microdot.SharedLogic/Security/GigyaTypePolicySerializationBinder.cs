#nullable enable

using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Newtonsoft.Json.Serialization;
using System;

namespace Gigya.Microdot.SharedLogic.Security
{
    public interface IMicrodotTypePolicySerializationBinder : ISerializationBinder
    {
        
    }
    
    public class MicrodotTypePolicySerializationBinder : DefaultSerializationBinder, IMicrodotTypePolicySerializationBinder
    {
        private readonly IMicrodotSerializationConstraints _serializationConstraints;
        
        public MicrodotTypePolicySerializationBinder(IMicrodotSerializationConstraints serializationConstraints)
        {
            _serializationConstraints = serializationConstraints;
        }


        public override Type BindToType(string? assemblyName, string typeName)
        {
            _serializationConstraints.ThrowIfExcluded(typeName);

            AssemblyAndTypeName assemblyAndTypeName = null;

            if (assemblyName != null && typeName != null)
                assemblyAndTypeName = _serializationConstraints.TryGetAssemblyNameAndTypeReplacement(assemblyName, typeName);
            else
                assemblyAndTypeName = new AssemblyAndTypeName(assemblyName, typeName);

            return base.BindToType(assemblyAndTypeName.AssemblyName, assemblyAndTypeName.TypeName);
        }

        public override void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            var serializedTypeFullName = serializedType.Assembly.FullName;
            
            _serializationConstraints.ThrowIfExcluded(serializedTypeFullName);
            
            base.BindToName(serializedType, out assemblyName, out typeName);

            AssemblyAndTypeName assemblyAndTypeName = null;

            if (assemblyName != null && typeName != null)
                assemblyAndTypeName = _serializationConstraints.TryGetAssemblyAndTypeNameReplacementFromType(serializedType, assemblyName, typeName);
            else
                assemblyAndTypeName = new AssemblyAndTypeName(assemblyName, typeName);

            assemblyName = assemblyAndTypeName.AssemblyName;
            typeName = assemblyAndTypeName.TypeName;
        }
    }
}
