using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    internal class ExceptionHierarchySerializationBinder : DefaultSerializationBinder
    {
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
            }
        }

        private static IEnumerable<Type> GetInheritanceHierarchy(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
                yield return current;
        }
    }
}