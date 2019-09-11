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
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Ninject.Extensions.Conventions;
using Ninject.Syntax;

namespace Gigya.Microdot.Ninject
{
    public static class MicrodotConventions
    {
        public static void BindClassesAsSingleton(this IBindingRoot bindingRoot, IList<Type> nonSingletonBaseTypes = null, params Type[] assemblies)
        {
            var list = new List<Type>
            {
                typeof(IConfigObject),
                typeof(IEvent)
            };

            if (nonSingletonBaseTypes != null)
            {
                list.AddRange(nonSingletonBaseTypes);
            }

            bindingRoot.Bind(x =>
                {
                    x.FromAssemblyContaining(assemblies)
                    .SelectAllClasses()
                    .Where(t => list.All(nonSingletonType => nonSingletonType.IsAssignableFrom(t) == false))
                    .BindToSelf()
                    .Configure(c => c.InSingletonScope());
                });
        }


        public static void BindInterfacesAsSingleton(this IBindingRoot bindingRoot, IList<Type> nonSingletonBaseTypes, IList<Type> bindInterfacesInAssemblies, params Type[] assemblies)
        {
            var list = new List<Type>
            {
                typeof(IConfigObject),
                typeof(IEvent)
            };

            if (nonSingletonBaseTypes != null)
            {
                list.AddRange(nonSingletonBaseTypes);
            }

            bindingRoot.Bind(x =>
                {
                    x.FromAssemblyContaining(assemblies)
                    .SelectAllClasses()
                    .Where(t => list.All(nonSingletonType => nonSingletonType.IsAssignableFrom(t) == false))
                    // Bind interfaces to the implementation from assemblies ( by types )
                    // The interfaces are from the specific assemblies ( by types as well)
                    // The last is to avoid bind types arbitrary and isolate it to same assembly or abstraction assembly.
                    .BindSelection((type, types) => 
                        types.Where(i => assemblies.Select(a => a.Assembly).Contains(i.Assembly) || 
                                         bindInterfacesInAssemblies?.Select(a => a.Assembly).Contains(i.Assembly) == true))
                    .Configure(c => c.InSingletonScope());
                });
        }


    }
}