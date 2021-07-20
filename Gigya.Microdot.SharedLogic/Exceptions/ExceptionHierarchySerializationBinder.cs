using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Gigya.Microdot.SharedLogic.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    public interface IExceptionHierarchySerializationBinder : ISerializationBinder
    {
        
    }
    
    public class ExceptionHierarchySerializationBinder : IExceptionHierarchySerializationBinder
    {
        private readonly IGigyaTypePolicySerializationBinder _gigyaBinder;

        public ExceptionHierarchySerializationBinder(IGigyaTypePolicySerializationBinder gigyaBinder)
        {
            _gigyaBinder = gigyaBinder;
        }
        public Type BindToType(string assemblyName, string typeName)
        {
            var assemblyNames = assemblyName.Split(':');
            var typeNames = typeName.Split(':');
            var type = assemblyNames.Zip(typeNames, TryBindToType).FirstOrDefault(t => t != null);

            return type ?? _gigyaBinder.BindToType(typeof(Exception).Assembly.GetName().Name, typeof(Exception).FullName);
        }

        private Type TryBindToType(string assemblyName, string typeName)
        {
            try
            {
                return _gigyaBinder.BindToType(assemblyName, typeName);
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
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
                _gigyaBinder.BindToName(serializedType, out assemblyName, out typeName);
            }
        }

        private static IEnumerable<Type> GetInheritanceHierarchy(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                yield return current;
        }
    }
}