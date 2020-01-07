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

using Gigya.Microdot.System_Reflection.DispatchProxy;
using System;
using System.Reflection;

namespace Gigya.Microdot.ServiceProxy
{

    /// <summary>
    /// A helper class used to redirect <see cref="DispatchProxy"/> calls to another location.
    /// </summary>
    /// <remarks>
    /// In order for proxy generation to succeed, this class must be public and have a parameterless constructor.
    /// </remarks>
    public class DelegatingDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> InvokeDelegate { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (InvokeDelegate == null)
                throw new InvalidOperationException($"You cannot use the {nameof(DelegatingDispatchProxy)} class without initializing its {nameof(InvokeDelegate)} property after instantiation.");

            return InvokeDelegate(targetMethod, args);
        }
    }
}