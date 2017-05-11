using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Attributes;


namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class MetadataProvider : IMetadataProvider
    {
        private ConcurrentDictionary<MethodInfo, Type> TaskReturnTypes { get; } = new ConcurrentDictionary<MethodInfo, Type>();
        private ConcurrentDictionary<MethodInfo, bool> IsMethodCached { get; } = new ConcurrentDictionary<MethodInfo, bool>();


        public Type GetMethodTaskResultType(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            return TaskReturnTypes.GetOrAdd(method, m => GetTaskResultType(m.ReturnType));
        }


        private Type GetTaskResultType(Type taskType)
        {
            if (taskType == null)
                throw new ArgumentNullException(nameof(taskType));

            if (taskType == typeof(Task))
                return null;

            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                return taskType.GetGenericArguments().First();

            throw new ArgumentException("The specified type is not a Task", nameof(taskType));
        }


        public bool IsCached(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));

            return IsMethodCached.GetOrAdd(methodInfo, m => m.GetCustomAttribute<CachedAttribute>() != null);
        }


        public bool HasCachedMethods(Type interfaceType)
        {
            var cachedMethods = interfaceType
                .GetMethods()
                .Where(IsCached)
                .ToArray();

            if (cachedMethods.Length == 0)
                return false;

            var invalidCachedMethods = cachedMethods
                .Where(m => m.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
                .ToArray();

            if (invalidCachedMethods.Any())
            {
                throw new ArgumentException("CachingProxy: All methods decorated with [Cached] must have a return value of Task<T>, " +
                    $"but the following methods of {interfaceType.FullName} do not: " +
                    string.Join(", ", invalidCachedMethods.AsEnumerable()));
            }

            return true;
        }
    }
}
