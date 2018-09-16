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
using System.Collections.Generic;
using System.Linq;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService.Endpoints;

namespace Gigya.Microdot.Hosting.HttpService
{
    /// <summary>
    /// Base implementation of IServiceInterfaceMapper
    /// </summary>
    public abstract class ServiceInterfaceMapper : IServiceInterfaceMapper
    {
        protected IEnumerable<Type> _serviceInterfaceTypes;

        public virtual IEnumerable<Type> ServiceInterfaceTypes => _serviceInterfaceTypes;
        public virtual IEnumerable<Type> ServiceClassesTypes { get; protected set; }

        public Type HealthStatusServiceType { get; set; }

        public abstract Type GetGrainInterface(Type serviceInterface);

        /// <summary>
        /// Extract the service implementing IHealthStatus        
        /// </summary>
        /// <exception cref="ProgrammaticException">throw exception with more than one service implemented IHealthStatus</exception>
        /// <param name="types"></param>
        protected void ExtractHealthStatusServiceType(IEnumerable<Type> types)
        {
            var healthStatusGrains = types.Where(i => i.GetInterface(typeof(IHealthStatus).Name) != null).ToArray();

            if (healthStatusGrains.Length > 1)
                throw new ProgrammaticException($"{typeof(IHealthStatus).Name} cannot be assigned to more than one service");

            HealthStatusServiceType = healthStatusGrains.FirstOrDefault();
        }
    }
}