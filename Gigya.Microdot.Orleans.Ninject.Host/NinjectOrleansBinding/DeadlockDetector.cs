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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Gigya.Microdot.Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ninject;
using Ninject.Activation;
using Ninject.Activation.Caching;
using Ninject.Parameters;
using Ninject.Planning.Bindings;
using Ninject.Planning.Targets;
using Ninject.Syntax;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Prevent form binding a Singleton dependency that depend on scoped dependency.
    /// Can cause deadlock when other Singleton dependency dependent on the same scope.
    /// </summary>
    public class DeadlockDetector
    {
       
        public static void validate(IServiceCollection services)
        {
            var scope = services.Where(x => x.Lifetime == ServiceLifetime.Scoped).ToLookup(x => x.ServiceType);
            Stack<Type> serviceDescriptors = new Stack<Type>();
            HashSet<Type> visit = new HashSet<Type>();
            var singelTone = services.Where(x => x.Lifetime == ServiceLifetime.Singleton);
            foreach (var service in singelTone)
            {
                foreach (var ctor in service.ServiceType.GetConstructors())
                {
                    if (visit.Contains(ctor.DeclaringType)) continue;

                    foreach (var pram in ctor.GetParameters())
                    {
                        var type = pram.ParameterType;
                        if (visit.Contains(type)) break;
                        if (scope.Contains(pram.ParameterType))
                        {
                            throw new DeadlockDetectorExeption("scope should not point to a singleton");
                        }
                        serviceDescriptors.Push(pram.ParameterType);

                    }
                }
            }
        }
    }
}