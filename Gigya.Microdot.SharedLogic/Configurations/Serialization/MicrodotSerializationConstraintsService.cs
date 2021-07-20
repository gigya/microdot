using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.Configurations.Serialization
{
    public interface IMicrodotSerializationConstraints
    {
        void ThrowIfExcluded(string typeName);
        MicrodotSerializationConstraints.AssemblyAndTypeName TryGetAssemblyNameAndTypeReplacement(string? assemblyName, string typeName);

        MicrodotSerializationConstraints.AssemblyAndTypeName TryGetAssemblyAndTypeNameReplacementFromType(Type serializedType,
            string assemblyName, string typeName);
    }

    public class MicrodotSerializationConstraints : IMicrodotSerializationConstraints
    {
        public class AssemblyAndTypeName
        {
            public string AssemblyName { get; }
            public string TypeName { get; }

            public AssemblyAndTypeName(string assemblyName, string typeName)
            {
                AssemblyName = assemblyName;
                TypeName = typeName;
            }
        }
        
        private readonly Func<MicrodotSerializationSecurityConfig> _getSerializationConfig;
        private ConcurrentDictionary<string, string> assemblyNameToFixedAssyemblyCache =
            new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> typeNameToFixedAssyemblyCache =
            new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<Type, AssemblyAndTypeName> typeToAssemblyCache =
            new ConcurrentDictionary<Type, AssemblyAndTypeName>();

        private MicrodotSerializationSecurityConfig _lastConfig;

        public MicrodotSerializationConstraints(Func<MicrodotSerializationSecurityConfig> getSerializationConfig)
        {
            _getSerializationConfig = getSerializationConfig;
        }

        public void ThrowIfExcluded(string typeName)
        {
            foreach (var deserializationForbiddenType in _getSerializationConfig().DeserializationForbiddenTypes)
            {
                if (typeName.IndexOf(deserializationForbiddenType, StringComparison.InvariantCultureIgnoreCase) >=0)
                    throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToType type '{typeName}'");    
            }
        }
        
        public AssemblyAndTypeName TryGetAssemblyNameAndTypeReplacement(string? assemblyName, string typeName)
        {
            var config = GetSerializationConfigAndRefreshCaches();

            // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
            // https://programmingflow.com/2020/02/18/could-not-load-system-private-corelib.html
            if (false == assemblyNameToFixedAssyemblyCache.TryGetValue(assemblyName, out var localAssemblyName))
            {
                localAssemblyName = assemblyName;

                foreach (var assemblyNameToRegexReplacement in config.AssemblyNamesRegexReplacements)
                {
                    if (localAssemblyName.IndexOf(
                        assemblyNameToRegexReplacement.AssemblyToReplace,
                        StringComparison.InvariantCultureIgnoreCase
                    ) >= 0)
                    {
                        localAssemblyName = assemblyNameToRegexReplacement.AssemblyRegularExpression
                            .Replace(localAssemblyName, assemblyNameToRegexReplacement.AssemblyReplacement);

                        assemblyNameToFixedAssyemblyCache.TryAdd(assemblyName, localAssemblyName);

                        if (localAssemblyName != assemblyName)
                        {
                            assemblyName = localAssemblyName;
                            break;
                        }
                    }
                }
            }
            else
            {
                assemblyName = localAssemblyName;
            }

            if (false == typeNameToFixedAssyemblyCache.TryGetValue(typeName, out var localTypeName))
            {
                localTypeName = typeName;

                foreach (var assemblyNameToRegexReplacement in config.AssemblyNamesRegexReplacements)
                {
                    if (localTypeName.IndexOf(
                        assemblyNameToRegexReplacement.AssemblyToReplace,
                        StringComparison.InvariantCultureIgnoreCase
                    ) >= 0)
                    {
                        localTypeName = assemblyNameToRegexReplacement.AssemblyRegularExpression
                            .Replace(localTypeName, assemblyNameToRegexReplacement.AssemblyReplacement);

                        typeNameToFixedAssyemblyCache.TryAdd(typeName, localTypeName);

                        if (localTypeName != typeName)
                        {
                            typeName = localTypeName;
                            break;
                        }
                    }
                }
            }
            else
            {
                typeName = localTypeName;
            }

            return new AssemblyAndTypeName(assemblyName, typeName);
        }

        private MicrodotSerializationSecurityConfig GetSerializationConfigAndRefreshCaches()
        {
            var config = _getSerializationConfig();

            if (config != _lastConfig)
            {
                assemblyNameToFixedAssyemblyCache = new ConcurrentDictionary<string, string>();
                typeNameToFixedAssyemblyCache = new ConcurrentDictionary<string, string>();
                typeToAssemblyCache = new ConcurrentDictionary<Type, AssemblyAndTypeName>();
            }

            _lastConfig = config;
            return config;
        }


        public AssemblyAndTypeName TryGetAssemblyAndTypeNameReplacementFromType(Type serializedType, 
            string assemblyName, string typeName)
        {
            var config = GetSerializationConfigAndRefreshCaches();

            // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
            // https://programmingflow.com/2020/02/18/could-not-load-system-private-corelib.html
        
            // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
            if (typeToAssemblyCache.TryGetValue(serializedType, out var assemblyNameAndTypeName))
            {
                return assemblyNameAndTypeName;
            }

            var localTypeName = typeName;
            var localAssemblyName = assemblyName;

            foreach (var assemblyNameToRegexReplacement in config.AssemblyNamesRegexReplacements)
            {
                if (localTypeName.IndexOf(
                    assemblyNameToRegexReplacement.AssemblyToReplace,
                    StringComparison.InvariantCultureIgnoreCase
                ) >= 0)
                {
                    localTypeName = assemblyNameToRegexReplacement.AssemblyRegularExpression
                        .Replace(localTypeName, assemblyNameToRegexReplacement.AssemblyReplacement);
                }

                if (localTypeName != typeName)
                {
                    typeName = localTypeName;
                    break;
                }
            }
                
            foreach (var assemblyNameToRegexReplacement in config.AssemblyNamesRegexReplacements)
            {
                if (localAssemblyName.IndexOf(
                    assemblyNameToRegexReplacement.AssemblyToReplace,
                    StringComparison.InvariantCultureIgnoreCase
                ) >= 0)
                {
                    localAssemblyName = assemblyNameToRegexReplacement.AssemblyRegularExpression
                        .Replace(localAssemblyName, assemblyNameToRegexReplacement.AssemblyReplacement);

                }

                if (localAssemblyName != assemblyName)
                {
                    assemblyName = localAssemblyName;
                    break;
                }
            }
                
            typeToAssemblyCache.TryAdd(serializedType, new AssemblyAndTypeName(assemblyName, typeName));

            return new AssemblyAndTypeName(assemblyName, typeName);
        }
        
    }
}