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
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Hosting.Validators
{
    public class LogFieldAttributeValidator : IValidator
    {
        private readonly IServiceInterfaceMapper _serviceInterfaceMapper;
        private readonly Type[] _typesPrecludedFromLogFieldAttributeUsage;

        public LogFieldAttributeValidator(IServiceInterfaceMapper serviceInterfaceMapper)
        {
            _serviceInterfaceMapper = serviceInterfaceMapper;

            _typesPrecludedFromLogFieldAttributeUsage = new[]
            {
                typeof(string), typeof(JToken), typeof(Type)
            };
        }

        public void Validate()
        {
            foreach (var serviceInterface in _serviceInterfaceMapper.ServiceInterfaceTypes)
            {
                foreach (var method in serviceInterface.GetMethods())
                {
                    LogFieldAppliedOnlyOnClassParameters(serviceInterface, method);
                }
            }
        }

        private void LogFieldAppliedOnlyOnClassParameters(Type serviceInterface, MethodInfo method)
        {
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.GetCustomAttribute(typeof(LogFieldsAttribute)) != null)
                {
                    if (parameter.ParameterType.IsClass == false || _typesPrecludedFromLogFieldAttributeUsage.Any(x => x == parameter.ParameterType))
                    {
                        throw new ProgrammaticException($"LogFieldAttribute cannot be applied to parameter '{parameter.Name}' of method '{method.Name}' on '{serviceInterface.Name}'. It can only be applied to reference types, except the following types: ${string.Join(", ", _typesPrecludedFromLogFieldAttributeUsage.Select(x => x.Name))}");
                    }
                }
            }
        }
    }
}