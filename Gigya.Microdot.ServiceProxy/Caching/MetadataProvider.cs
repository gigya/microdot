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
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Attributes;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class MetadataProvider : IMetadataProvider
    {
        private ConcurrentDictionary<MethodInfo, Type> TaskReturnTypes { get; } = new ConcurrentDictionary<MethodInfo, Type>();
        private ConcurrentDictionary<MethodInfo, CachedAttribute> CachedAttributePerMethod { get; } = new ConcurrentDictionary<MethodInfo, CachedAttribute>();


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


        public bool IsCached(MethodInfo methodInfo) => GetCachedAttribute(methodInfo) != null;


        public CachedAttribute GetCachedAttribute(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));

            return CachedAttributePerMethod.GetOrAdd(methodInfo, m => m.GetCustomAttribute<CachedAttribute>());
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
