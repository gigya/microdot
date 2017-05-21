#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

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
