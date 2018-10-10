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

namespace Gigya.Microdot.ServiceProxy
{
    public static class ServiceProxyExtensions
    {
        public static string GetServiceName(this Type serviceInterfaceType)
        {
            var assemblyName = serviceInterfaceType.Assembly.GetName().Name;
            var endIndex = assemblyName.IndexOf(".Interface", StringComparison.OrdinalIgnoreCase);
            if (endIndex <= 0)
                return GetServiceNameFromTypeName(serviceInterfaceType.Name) ?? 
                    serviceInterfaceType.FullName.Replace('+', '-'); 

            var startIndex = assemblyName.Substring(0, endIndex).LastIndexOf(".", StringComparison.OrdinalIgnoreCase) + 1;
            var length = endIndex - startIndex;
            return assemblyName.Substring(startIndex, length);
        }

        private static string GetServiceNameFromTypeName(string typeName)
        {
            // if typeName is starts with 'I' letter and the following letter is an upper-case letter, 
            // then it represents an interface name, like 'IDemoService', and the 'I' letter should be ignored.
            if (typeName.Length > 1 && typeName[0] == 'I' && typeName[1] >= 'A' && typeName[1] <= 'Z')
                typeName = typeName.Substring(1);

            if (typeName.EndsWith("Service"))
                return typeName;
            else            
                return null;            
        }
    }
}
