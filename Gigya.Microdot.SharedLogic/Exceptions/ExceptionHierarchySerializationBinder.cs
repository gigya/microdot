using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    internal class ExceptionHierarchySerializationBinder : DefaultSerializationBinder
    {
        private readonly Func<ExceptionSerializationConfig> _getExceptionSerializationConfig;

        private static readonly Regex regex = new Regex(
            @"System\.Private\.CoreLib(, Version=[\d\.]+)?(, Culture=[\w-]+)?(, PublicKeyToken=[\w\d]+)?");

        private static readonly ConcurrentDictionary<Type, (string assembly, string type)> typeToAssemblyCache =
            new ConcurrentDictionary<Type, (string, string)>();
        
        private static readonly ConcurrentDictionary<string, string> assemblyNameToFixedAssyemblyCache =
            new ConcurrentDictionary<string, string>();
        
        private static readonly ConcurrentDictionary<string, string> typeNameToFixedAssyemblyCache =
            new ConcurrentDictionary<string, string>();

        public ExceptionHierarchySerializationBinder(Func<ExceptionSerializationConfig> getExceptionSerializationConfig)
        {
            _getExceptionSerializationConfig = getExceptionSerializationConfig;
        }
        public override Type BindToType(string assemblyName, string typeName)
        {
            var assemblyNames = assemblyName.Split(':');
            var typeNames = typeName.Split(':');
            var type = assemblyNames.Zip(typeNames, TryBindToType).FirstOrDefault(t => t != null);

            return type ?? base.BindToType(typeof(Exception).Assembly.GetName().Name, typeof(Exception).FullName);
        }

        private Type TryBindToType(string assemblyName, string typeName)
        {
            try
            {
                if (_getExceptionSerializationConfig().UseNetCoreToFrameworkTypeTranslation)
                {
                    // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
                    // https://programmingflow.com/2020/02/18/could-not-load-system-private-corelib.html
                    if (false == assemblyNameToFixedAssyemblyCache.TryGetValue(assemblyName, out var localAssemblyName))
                    {
                        localAssemblyName = assemblyName;

                        if (localAssemblyName.AsSpan().Contains("System.Private.CoreLib".AsSpan(),
                            StringComparison.OrdinalIgnoreCase))
                            localAssemblyName = regex.Replace(localAssemblyName, "mscorlib");

                        assemblyNameToFixedAssyemblyCache.TryAdd(assemblyName, localAssemblyName);
                    }

                    if (false == typeNameToFixedAssyemblyCache.TryGetValue(typeName, out var localTypeName))
                    {
                        localTypeName = typeName;

                        if (localTypeName.AsSpan().Contains("System.Private.CoreLib".AsSpan(),
                            StringComparison.OrdinalIgnoreCase))
                            localTypeName = regex.Replace(localTypeName, "mscorlib");

                        typeNameToFixedAssyemblyCache.TryAdd(typeName, localTypeName);
                    }
                    
                    return base.BindToType(localAssemblyName, localTypeName);
                }

                return base.BindToType(assemblyName, typeName);
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            if (serializedType.IsSubclassOf(typeof(Exception)))
            {
                var inheritanceHierarchy = GetInheritanceHierarchy(serializedType)
                    .Where(t => t.IsAbstract == false && t != typeof(object) && t != typeof(Exception))
                    .ToArray();

                typeName = string.Join(":", inheritanceHierarchy.Select(t => t.FullName));
                assemblyName = string.Join(":", inheritanceHierarchy.Select(t => t.Assembly.GetName().Name));
            }
            else
            {
                base.BindToName(serializedType, out assemblyName, out typeName);

                // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
                // https://programmingflow.com/2020/02/18/could-not-load-system-private-corelib.html
                if (_getExceptionSerializationConfig().UseNetCoreToFrameworkNameTranslation)
                {
                    // treat deserialization of net core System.Private.CoreLib library classes to net framework counterparts
                    if (typeToAssemblyCache.TryGetValue(serializedType, out var name))
                    {
                        assemblyName = name.assembly;
                        typeName = name.type;
                    }
                    else
                    {
                        if (assemblyName.AsSpan().Contains("System.Private.CoreLib".AsSpan(),
                            StringComparison.OrdinalIgnoreCase))
                            assemblyName = regex.Replace(assemblyName, "mscorlib");

                        if (typeName.AsSpan().Contains("System.Private.CoreLib".AsSpan(),
                            StringComparison.OrdinalIgnoreCase))
                            typeName = regex.Replace(typeName, "mscorlib");

                        typeToAssemblyCache.TryAdd(serializedType, (assemblyName, typeName));
                    }
                }
            }
        }

        private static IEnumerable<Type> GetInheritanceHierarchy(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                yield return current;
        }
    }
}