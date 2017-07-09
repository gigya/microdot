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
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{
	/// <summary>
	/// A representation of a service method which contains the method to be invoked, and in Orleans, the grain interface type too.
	/// </summary>
    public class ServiceMethod
    {
		/// <summary>The type of the grain interface, used in Orleans to create a grain reference (not used elsewhere)
		/// In Orleans, it <b>MUST</b> be the grain interface (e.g. IDemoGrain).</summary>
		public Type GrainInterfaceType { get;  }

        /// <summary>
		/// The method which should be activated by IActivator. It <b>MUST</b> be a method on a type decorated by <see cref="HttpServiceAttribute" />.
		/// In Orleans, this is the service interface (e.g. IDemoService).
        /// </summary>
        public MethodInfo ServiceInterfaceMethod { get; }

		/// <summary>
		/// True if the method is compatible with TAP-based calling convention, otherwise false.
		/// </summary>
        public bool IsCompatible { get;  }

        internal ServiceMethod(Type grainInterfaceType, MethodInfo serviceInterfaceMethod)
        {
            GrainInterfaceType = grainInterfaceType;

            ServiceInterfaceMethod = serviceInterfaceMethod;

            IsCompatible = typeof(Task).IsAssignableFrom(serviceInterfaceMethod.ReturnType);
        }
    }
}