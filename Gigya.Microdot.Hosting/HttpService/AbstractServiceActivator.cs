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
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Common.Contracts;

namespace Gigya.Microdot.Hosting.HttpService
{
    public abstract class AbstractServiceActivator : IActivator
    {
        public async Task<InvocationResult> Invoke(ServiceMethod serviceMethod, IDictionary args)
        {
            var arguments = GetParametersByName(serviceMethod, args);

            return await Invoke(serviceMethod, arguments).ConfigureAwait(false);
        }

        private static object[] GetParametersByName(ServiceMethod serviceMethod, IDictionary args)
        {
            return serviceMethod.ServiceInterfaceMethod
                .GetParameters()
                .Select(p => args[p.Name] ?? (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null))
                .ToArray();
        }

        public async Task<InvocationResult> Invoke(ServiceMethod serviceMethod, object[] arguments)
        {
            var invokeTarget = GetInvokeTarget(serviceMethod);

            object[] argumentsWithDefaults = GetConvertedAndDefaultArguments(serviceMethod.ServiceInterfaceMethod, arguments);

            var sw = Stopwatch.StartNew();
            var task = (Task)serviceMethod.ServiceInterfaceMethod.Invoke(invokeTarget, argumentsWithDefaults);
            await task.ConfigureAwait(false);
            var executionTime = sw.Elapsed;
            var result = task.GetType().GetProperty("Result").GetValue(task);

            return new InvocationResult { Result = result, ExecutionTime = executionTime };
        }

        private static object[] GetConvertedAndDefaultArguments(MethodInfo method, object[] arguments)
        {
            var parameters = method.GetParameters();

            object[] argumentsWithDefaults = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                object argument = null;
                var param = parameters[i];

                if (i < arguments.Length)
                {
                    argument = JsonHelper.ConvertWeaklyTypedValue(arguments[i], param.ParameterType);
                }
                else
                {                    
                    if (param.IsOptional)
                    {
                        if (param.HasDefaultValue)
                            argument = param.DefaultValue;
                        else if (param.ParameterType.IsValueType)
                            argument = Activator.CreateInstance(param.ParameterType);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Call to method {method.Name} is missing argument for {param.Name} and this paramater is not optional.");
                    }                   
                }

                argumentsWithDefaults[i] = argument;
            }

            return argumentsWithDefaults;
        }

        protected abstract object GetInvokeTarget(ServiceMethod serviceMethod);        
    }
}