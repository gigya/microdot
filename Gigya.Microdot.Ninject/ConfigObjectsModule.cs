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
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Extensions.Factory;
using Ninject.Modules;
using Ninject.Parameters;

namespace Gigya.Microdot.Ninject
{
    public class ConfigObjectsModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Rebind<IConfigObjectCreator>().To<ConfigObjectCreator>().InTransientScope();
            //Kernel.Rebind<IConfigObjectCreatorWrapper>().To<ConfigObjectCreatorWrapper>().InTransientScope();
            Kernel.Bind<IConfigEventFactory>().To<ConfigEventFactory>();
            Kernel.Bind<IConfigFuncFactory>().ToFactory();

            //SearchAssembliesAndRebindIConfig(Kernel);
        }

        private void SearchAssembliesAndRebindIConfig(IKernel kernel)
        {
            IAssemblyProvider aProvider = kernel.Get<IAssemblyProvider>();
            foreach (Assembly assembly in aProvider.GetAssemblies())
            {
                foreach (Type configType in assembly.GetTypes().Where(ConfigObjectCreator.IsConfigObject))
                {
                    IConfigObjectCreatorWrapper cocWrapper = kernel.Get<IConfigObjectCreatorWrapper>(new ConstructorArgument("type", configType));

                    dynamic getLataestLambda = cocWrapper.GetLambdaOfGetLatest(configType);
                    kernel.Rebind(typeof(Func<>).MakeGenericType(configType)).ToMethod(t => getLataestLambda());

                    Type sourceBlockType = typeof(ISourceBlock<>).MakeGenericType(configType);
                    kernel.Rebind(sourceBlockType).ToMethod(m => cocWrapper.GetChangeNotifications());

                    dynamic changeNotificationsLambda = cocWrapper.GetLambdaOfChangeNotifications(sourceBlockType);
                    kernel.Rebind(typeof(Func<>).MakeGenericType(sourceBlockType)).ToMethod(i => changeNotificationsLambda());

                    kernel.Rebind(configType).ToMethod(i => cocWrapper.GetLatest());
                }
            }
        }
    }
}
