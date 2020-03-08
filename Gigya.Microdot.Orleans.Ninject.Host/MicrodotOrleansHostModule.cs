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

using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.SharedLogic;
using Ninject.Modules;
using Orleans;
using System;
using System.Collections.Generic;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding;
using Orleans.Runtime;
using Orleans.Serialization;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    /// <summary>
    /// Binding needed for Orleans based host
    /// </summary>
    public class MicrodotOrleansHostModule : NinjectModule
    {
        public override void Load()
        {

            // note this is not my Assembly
            this.BindClassesAsSingleton(new[] { typeof(Grain) }, typeof(OrleansHostingAssembly));
            this.BindInterfacesAsSingleton(new[] { typeof(Grain) }, new List<Type> { typeof(ILog) }, typeof(OrleansHostingAssembly));
            // note this is not my Assembly


            Rebind<IActivator>().To<GrainActivator>().InSingletonScope();
            Rebind<IWorker>().To<ProcessingGrainWorker>().InSingletonScope();
            Rebind<IServiceInterfaceMapper>().To<OrleansServiceInterfaceMapper>().InSingletonScope();
            Bind<IOrleansToNinjectBinding>().To<OrleansToNinjectBinding>().InSingletonScope();
            Rebind<IWarmup>().To<GrainsWarmup>().InSingletonScope();
            Rebind<BaseCommonConfig, OrleansCodeConfig>().To<OrleansCodeConfig>().InSingletonScope();

            Rebind<IExternalSerializer, OrleansCustomSerialization>().To<OrleansCustomSerialization>().InSingletonScope();

            // Register logger per category
            Kernel.BindPerString<OrleansLogAdapter>();
            Rebind<IMetricTelemetryConsumer>().To<MetricsStatisticsConsumer>().InSingletonScope();
        }
    }
}
