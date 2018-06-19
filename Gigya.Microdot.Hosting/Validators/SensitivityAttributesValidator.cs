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
using System.Reflection.Emit;
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
                        {
                            try
                            {
                                VerifyMisplacedSensitivityAttribute(parameter.ParameterType, logFieldExists, Level);
                            }
                            catch (ArgumentException ex)
                            {
                                // better message also explaining attributes can't be put on nested members
                                throw new ProgrammaticException(
                                    $"[Sensitive] and [NonSensitive] should not be applied on a Property of an complex parameter without LogField Attribute" +
                                    ex.Message);
                            }
                        }
                    }
                }
            }
        }

        private void VerifyMisplacedSensitivityAttribute(Type type, bool logFieldExists, int level)
        {

            if (type.IsClass == false || type.FullName?.StartsWith("System.") == true)
                return;

            foreach (var memberInfo in type.FindMembers(MemberTypes.Property | MemberTypes.Field, BindingFlags.Public | BindingFlags.Instance, null, null)
                                           .Where(x => x is FieldInfo || ((x is PropertyInfo propertyInfo) && propertyInfo.CanRead)))
            {
                Exception reason = null;

                if (IsLegitimate(memberInfo, logFieldExists, level, ref  reason) == false)
                    throw new ArgumentException($"On {type.FullName}::{memberInfo.Name} the following error occured ==> [{reason.Message}]");

                try
                {
                    if (memberInfo is PropertyInfo propertyInfo)
                        VerifyMisplacedSensitivityAttribute(propertyInfo.PropertyType, logFieldExists, level + 1);
                    else
                        VerifyMisplacedSensitivityAttribute(((FieldInfo)memberInfo).FieldType, logFieldExists, level + 1);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException(type.FullName + " --> " + ex.Message);
                }
            }
        }

        private bool IsLegitimate(MemberInfo memberInfo, bool logFieldExists, int level, ref Exception reason)
        {
            var attribute = memberInfo.GetCustomAttribute(typeof(SensitiveAttribute)) ?? memberInfo.GetCustomAttribute(typeof(NonSensitiveAttribute));

            if (attribute != null)
            {
                if (logFieldExists)
                {
                    reason = new SensitivityAttributeInWrongLevelException(attribute.GetType().Name, Level, level);
                    return level == Level;
                }

                reason = new SensitivityAttributeExistsWithoutLogFieldAttribute(attribute.GetType().Name);
                return false;
            }

            return true;
        }

        private class SensitivityAttributeExistsWithoutLogFieldAttribute : Exception
        {
            public SensitivityAttributeExistsWithoutLogFieldAttribute(string attribute)
                : base($"{attribute} appears when LogField is missing - Invalid behaviour")
            {

            }
        }


        private class SensitivityAttributeInWrongLevelException : Exception
        {

            public SensitivityAttributeInWrongLevelException(string attribute, int expectedLevel, int actualLevel)
                    : base($"{attribute} Should have been on {expectedLevel} depth but was on {actualLevel} depth - Invalid behaviour")

            {
            }
        }

    }
}