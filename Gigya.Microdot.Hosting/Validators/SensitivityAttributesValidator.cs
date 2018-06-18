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
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.Validators
{
    public class SensitivityAttributesValidator : IValidator
    {
        private readonly IServiceInterfaceMapper _serviceInterfaceMapper;
        private const int Level = 1;

        public SensitivityAttributesValidator(IServiceInterfaceMapper serviceInterfaceMapper)
        {
            _serviceInterfaceMapper = serviceInterfaceMapper;
        }

        public void Validate()
        {
            foreach (var serviceInterface in _serviceInterfaceMapper.ServiceInterfaceTypes)
            {
                foreach (var method in serviceInterface.GetMethods())
                {
                    if (method.GetCustomAttribute(typeof(SensitiveAttribute)) != null && method.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                        throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same method ({method.Name}) on serviceInterface ({serviceInterface.Name})");

                    foreach (var parameter in method.GetParameters())
                    {
                        if (parameter.GetCustomAttribute(typeof(SensitiveAttribute)) != null && parameter.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                        {
                            throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same parameter ({parameter.Name}) in method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
                        }

                        var logFieldExists = Attribute.IsDefined(parameter, typeof(LogFieldsAttribute));

                        if (parameter.ParameterType.IsClass && parameter.ParameterType.FullName?.StartsWith("System.") == false)
                            SearchSensitivityAttribute(parameter.ParameterType, logFieldExists, Level);
                    }
                }
            }
        }

        private void SearchSensitivityAttribute(Type type, bool logFieldExists, int level)
        {
            if (type.IsClass == false || type == typeof(string))
            {
                return;
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (IsLegitimate(property, logFieldExists, level) == false)
                {
                    throw new ProgrammaticException($"[SensitiveAttribute] should not be applied on a Property of an complex parameter withot LogField Attribute");
                }

                SearchSensitivityAttribute(property.PropertyType, logFieldExists, level + 1);
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (IsLegitimate(field, logFieldExists, level) == false)
                {
                    throw new ProgrammaticException($"[SensitiveAttribute] should not be applied on a Field of an complex parameter withot LogField Attribute");
                }

                SearchSensitivityAttribute(field.FieldType, logFieldExists, level + 1);
            }
        }

        private bool IsLegitimate(MemberInfo memberInfo, bool logFieldExists, int level)
        {
            if (Attribute.IsDefined(memberInfo, typeof(SensitiveAttribute)))
            {
                if (logFieldExists)
                {
                    return level == Level;
                }
                return false;
            }

            return true;
        }
    }
}