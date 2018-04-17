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
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.HttpService
{
    public class EndPointMetadata
    {
        public EndPointMetadata(ServiceMethod method)
        {
            Initialize(method);
        }

        public ImmutableDictionary<string, (Sensitivity? Sensitivity, bool IsLogFieldAttributeExists)> ParameterAttributes { get; private set; }

        public Sensitivity? MethodSensitivity { get; private set; }


        private void Initialize(ServiceMethod method)
        {

            MethodSensitivity = GetSensitivity(method.ServiceInterfaceMethod);

            var parameterAttributes = ImmutableDictionary.CreateBuilder<string, (Sensitivity? sensitivity, bool isLogFieldAttributeExists)>();

            foreach (var param in method.ServiceInterfaceMethod.GetParameters())
            {
                var tuple = (Sensitivity: GetSensitivity(param), IsLogFieldAttributeExists: param.GetCustomAttribute<LogFieldsAttribute>() != null);
                parameterAttributes.Add(param.Name, tuple);
            }

            ParameterAttributes = parameterAttributes.ToImmutable();
        }

        private Sensitivity? GetSensitivity(ICustomAttributeProvider t)
        {

            var attribute = t.GetCustomAttributes(typeof(SensitiveAttribute), true).FirstOrDefault();
            if (attribute is SensitiveAttribute sensitiveAttribute)
            {
                if (sensitiveAttribute.Secretive)
                    return Sensitivity.Secretive;

                return Sensitivity.Sensitive;
            }

            attribute = t.GetCustomAttributes(typeof(NonSensitiveAttribute), true).FirstOrDefault();
            if (attribute is NonSensitiveAttribute)
            {
                return Sensitivity.NonSensitive;
            }
            return null;
        }
    }

}