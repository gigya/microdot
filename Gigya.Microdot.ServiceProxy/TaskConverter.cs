using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceProxy
{
    internal static class TaskConverter
    {
        private static ConcurrentDictionary<Type, Func<Task<object>, Task>> ToStrongTypeConverters { get; } = new ConcurrentDictionary<Type, Func<Task<object>, Task>>();
        private static ConcurrentDictionary<Type, Func<Task, Task<object>>> ToWeakTypeConverters { get; } = new ConcurrentDictionary<Type, Func<Task, Task<object>>>();

        internal static Task ToStronglyTypedTask(Task<object> weaklyTypedTask, Type resultType)
        {
            if (resultType == typeof(object))
                return weaklyTypedTask;

            var taskTypeConverter = ToStrongTypeConverters.GetOrAdd(resultType, CreateStrongTypeConverter);

            return taskTypeConverter(weaklyTypedTask);
        }

        internal static Task<object> ToWeaklyTypedTask(Task stronglyTypedTask, Type resultType)
        {
            if (stronglyTypedTask is Task<object>)
                return (Task<object>)stronglyTypedTask;

            var taskTypeConverter = ToWeakTypeConverters.GetOrAdd(resultType, CreateWeakTypeConverter);

            return taskTypeConverter(stronglyTypedTask);
        }

        private static Func<Task<object>, Task> CreateStrongTypeConverter(Type resultType)
        {
            var inputTaskParam = Expression.Parameter(typeof(Task<object>), "inputTask");
            var method = typeof(TaskConverter).GetMethod(nameof(AsTaskOf), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(resultType);
            var methodCall = Expression.Call(method, inputTaskParam);
            var cast = Expression.Convert(methodCall, typeof(Task));
            var lambda = Expression.Lambda<Func<Task<object>, Task>>(cast, inputTaskParam);
            return lambda.Compile();
        }

        private static Func<Task, Task<object>> CreateWeakTypeConverter(Type resultType)
        {
            var inputTaskParam = Expression.Parameter(typeof(Task), "inputTask");
            var cast = Expression.Convert(inputTaskParam, typeof(Task<>).MakeGenericType(resultType));
            var method = typeof(TaskConverter).GetMethod(nameof(AsObjectTaskFrom), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(resultType);
            var methodCall = Expression.Call(method, cast);
            var lambda = Expression.Lambda<Func<Task, Task<object>>>(methodCall, inputTaskParam);
            return lambda.Compile();
        }

        private static async Task<T> AsTaskOf<T>(Task<object> task) { return (T)await task.ConfigureAwait(false); }

        private static async Task<object> AsObjectTaskFrom<T>(Task<T> task) { return await task.ConfigureAwait(false); }
    }
}
