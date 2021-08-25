using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Gigya.Microdot.SharedLogic.Configurations.Serialization
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
    
    public interface IMicrodotSerializationConstraints
    {
        void ThrowIfExcluded(string typeName);
#nullable enable
        AssemblyAndTypeName TryGetAssemblyNameAndTypeReplacement(string? assemblyName, string typeName);
#nullable disable
        AssemblyAndTypeName TryGetAssemblyAndTypeNameReplacementFromType(Type serializedType,
            string assemblyName, string typeName);
    }

    public class MicrodotSerializationConstraints : IMicrodotSerializationConstraints
    {
        public class MicrodotSerializationEffectiveConfiguration
        {
            private MicrodotSerializationSecurityConfig _lastConfig;
            
            public List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement> RegexReplacements { get; }
            public List<string> ForbiddenTypes { get; }

            public ConcurrentDictionary<string, string> AssemblyNameToFixedAssyemblyCache { get; }
            public ConcurrentDictionary<string, string> TypeNameToFixedAssyemblyCache { get; }
            public ConcurrentDictionary<Type, AssemblyAndTypeName> TypeToAssemblyCache { get; }

            public MicrodotSerializationEffectiveConfiguration(MicrodotSerializationSecurityConfig serializationConfig)
            {
                _lastConfig = serializationConfig;

                RegexReplacements = serializationConfig.AssemblyNamesRegexReplacements?? new List<MicrodotSerializationSecurityConfig.AssemblyNameToRegexReplacement>();
                ForbiddenTypes = serializationConfig.DeserializationForbiddenTypes?? new List<string>();
                AssemblyNameToFixedAssyemblyCache = new ConcurrentDictionary<string, string>();
                TypeNameToFixedAssyemblyCache = new ConcurrentDictionary<string, string>();
                TypeToAssemblyCache = new ConcurrentDictionary<Type, AssemblyAndTypeName>();
            }

            public bool WasConfigChanged(MicrodotSerializationSecurityConfig serializationConfig)
            {
                return _lastConfig != serializationConfig;
            }
        }
        
      
        private readonly Func<MicrodotSerializationSecurityConfig> _getSerializationConfig;

        private MicrodotSerializationEffectiveConfiguration _effectiveConfigCache;

        public MicrodotSerializationConstraints(Func<MicrodotSerializationSecurityConfig> getSerializationConfig)
        {
            _getSerializationConfig = getSerializationConfig;
        }

        public void ThrowIfExcluded(string typeName)
        {
            var config = GetSerializationConfigAndRefreshCaches();

            foreach (var deserializationForbiddenType in config.ForbiddenTypes)
            {
                if (typeName.IndexOf(deserializationForbiddenType, StringComparison.InvariantCultureIgnoreCase) >=0)
                    throw new UnauthorizedAccessException($"JSON Serialization Binder forbids BindToType type '{typeName}'");    
            }
        }

#nullable enable
        public AssemblyAndTypeName TryGetAssemblyNameAndTypeReplacement(string? assemblyName, string typeName)
#nullable disable
        {
            var config = GetSerializationConfigAndRefreshCaches();

            // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
            // https://programmingflow.com/2020/02/18/could-not-load-system-private-corelib.html
            if (false == config.AssemblyNameToFixedAssyemblyCache.TryGetValue(assemblyName, out var localAssemblyName))
            {
                localAssemblyName = assemblyName;

                foreach (var assemblyNameToRegexReplacement in config.RegexReplacements)
                {
                    if (localAssemblyName.IndexOf(
                        assemblyNameToRegexReplacement.AssemblyToReplace,
                        StringComparison.InvariantCultureIgnoreCase
                    ) >= 0)
                    {
                        localAssemblyName = assemblyNameToRegexReplacement.AssemblyRegularExpression
                            .Replace(localAssemblyName, assemblyNameToRegexReplacement.AssemblyReplacement);

                        config.AssemblyNameToFixedAssyemblyCache.TryAdd(assemblyName, localAssemblyName);

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

            if (false == config.TypeNameToFixedAssyemblyCache.TryGetValue(typeName, out var localTypeName))
            {
                localTypeName = typeName;

                foreach (var assemblyNameToRegexReplacement in config.RegexReplacements)
                {
                    if (localTypeName.IndexOf(
                        assemblyNameToRegexReplacement.AssemblyToReplace,
                        StringComparison.InvariantCultureIgnoreCase
                    ) >= 0)
                    {
                        localTypeName = assemblyNameToRegexReplacement.AssemblyRegularExpression
                            .Replace(localTypeName, assemblyNameToRegexReplacement.AssemblyReplacement);

                        config.TypeNameToFixedAssyemblyCache.TryAdd(typeName, localTypeName);

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

        private MicrodotSerializationEffectiveConfiguration GetSerializationConfigAndRefreshCaches()
        {
            var config = _getSerializationConfig();

            if (_effectiveConfigCache == null)
                _effectiveConfigCache = new MicrodotSerializationEffectiveConfiguration(config);
            else if (_effectiveConfigCache.WasConfigChanged(config))
                _effectiveConfigCache = new MicrodotSerializationEffectiveConfiguration(config);

            return _effectiveConfigCache;
        }


        public AssemblyAndTypeName TryGetAssemblyAndTypeNameReplacementFromType(Type serializedType, 
            string assemblyName, string typeName)
        {
            var config = GetSerializationConfigAndRefreshCaches();

            // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
            // https://programmingflow.com/2020/02/18/could-not-load-system-private-corelib.html
        
            // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
            if (config.TypeToAssemblyCache.TryGetValue(serializedType, out var assemblyNameAndTypeName))
            {
                return assemblyNameAndTypeName;
            }

            var localTypeName = typeName;
            var localAssemblyName = assemblyName;

            foreach (var assemblyNameToRegexReplacement in config.RegexReplacements)
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
                
            foreach (var assemblyNameToRegexReplacement in config.RegexReplacements)
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
                
            config.TypeToAssemblyCache.TryAdd(serializedType, new AssemblyAndTypeName(assemblyName, typeName));

            return new AssemblyAndTypeName(assemblyName, typeName);
        }
        
    }
}