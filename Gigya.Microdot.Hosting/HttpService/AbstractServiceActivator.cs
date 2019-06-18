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

#endregion Copyright

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService
{
    public abstract class AbstractServiceActivator : IActivator
    {
        private readonly ConcurrentDictionary<ServiceMethod, Func<object, object[], Task>> _fastInvoke = new ConcurrentDictionary<ServiceMethod, Func<object, object[], Task>>();
        private readonly ConcurrentDictionary<ServiceMethod, Func<Task, object>> _fastGetter = new ConcurrentDictionary<ServiceMethod, Func<Task, object>>();

        public async Task<InvocationResult> Invoke(ServiceMethod serviceMethod, object[] argumentsWithDefaults)
        {
            var invokeTarget = GetInvokeTarget(serviceMethod);

            var sw = Stopwatch.StartNew();
            var invoker = _fastInvoke.GetOrAdd(serviceMethod, (serviceMethod1) => CreateStrongTypeDelegate(serviceMethod.ServiceInterfaceMethod));
            var task = invoker(invokeTarget, argumentsWithDefaults);
            await task.ConfigureAwait(false);
            var executionTime = sw.Elapsed;

            var getTaskResult = _fastGetter.GetOrAdd(serviceMethod, CreateGetterForGetResult(task));
            var result = getTaskResult(task);

            return new InvocationResult { Result = result, ExecutionTime = executionTime };
        }

        public Func<object, object[], Task> CreateStrongTypeDelegate(MethodInfo methodInfo)
        {
            var methodParams = methodInfo.GetParameters();
            var arrayParameter = Expression.Parameter(typeof(object[]), "array");

            var arguments =
                methodParams.Select((p, i) => Expression.Convert(
                        Expression.ArrayAccess(arrayParameter, Expression.Constant(i)), p.ParameterType))
                    .Cast<Expression>()
                    .ToList();

            var instanceParameter = Expression.Parameter(typeof(object), "controller");

            var instanceExp = Expression.Convert(instanceParameter, methodInfo.DeclaringType);
            var callExpression = Expression.Call(instanceExp, methodInfo, arguments);

            var bodyExpression = Expression.Convert(callExpression, typeof(Task));

            return Expression.Lambda<Func<object, object[], Task>>(
                    bodyExpression, instanceParameter, arrayParameter)
                .Compile();
        }

        public Func<object, object> CreateGetterForGetResult(object task)
        {
            var inputTaskParam = Expression.Parameter(typeof(object), "inputTask");
            var convertToStrongTypeTask = Expression.ConvertChecked(inputTaskParam, task.GetType());
            var property = Expression.Property(convertToStrongTypeTask, nameof(Task<Object>.Result));
            var returnWeekType = Expression.Convert(property, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(returnWeekType, inputTaskParam).Compile();
            return lambda;
        }

        protected abstract object GetInvokeTarget(ServiceMethod serviceMethod);
    }
}