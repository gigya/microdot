﻿#region Copyright 
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
using System.Collections.Concurrent;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Factory;
using Ninject.Modules;

namespace Gigya.Microdot.Ninject
{
    /// <inheritdoc />
    /// <summary>
    /// Contains all binding except hosting layer
    /// </summary>
    public class MicrodotModule : NinjectModule
    {
    
        private readonly Type[] NonSingletonBaseTypes =
        {
            typeof(ConsulDiscoverySource),
            typeof(RemoteHostPool),
            typeof(ConfigDiscoverySource)
        };

        public override void Load()
        {
            //Need to be initialized before using any regex!
            new RegexTimeoutInitializer().Init();

            Kernel
                .Bind(typeof(ConcurrentDictionary<,>))
                .To(typeof(DisposableConcurrentDictionary<,>))
                .InSingletonScope();

            if (Kernel.CanResolve<Func<long, DateTime>>() == false)
                Kernel.Load<FuncModule>();

            this.BindClassesAsSingleton(NonSingletonBaseTypes, typeof(ConfigurationAssembly), typeof(ServiceProxyAssembly));
            this.BindInterfacesAsSingleton(NonSingletonBaseTypes, typeof(ConfigurationAssembly), typeof(ServiceProxyAssembly), typeof(SharedLogicAssembly),typeof(ServiceDiscoveryAssembly));

            Bind<IRemoteHostPoolFactory>().ToFactory();

            Kernel.BindPerKey<string, ReachabilityChecker, IServiceDiscovery, ServiceDiscovery.ServiceDiscovery>();
            Kernel.BindPerString<IServiceProxyProvider, ServiceProxyProvider>();
            Kernel.BindPerString<AggregatingHealthStatus>();

            Rebind<MetricsContext>()
                .ToMethod(c => Metric.Context(GetTypeOfTarget(c).Name))
                .InScope(GetTypeOfTarget);

            Rebind<IServiceDiscoverySource>().To<ConsulDiscoverySource>().InTransientScope();
            Bind<IServiceDiscoverySource>().To<LocalDiscoverySource>().InTransientScope();
            Bind<IServiceDiscoverySource>().To<ConfigDiscoverySource>().InTransientScope();

            Kernel.Rebind<IConsulClient>().To<ConsulClient>().InTransientScope();
            Kernel.Load<ServiceProxyModule>();
            Kernel.Load<ConfigObjectsModule>();
        }


        protected static Type GetTypeOfTarget(IContext context)
        {
            var type = context.Request.Target?.Member.DeclaringType;
            return type ?? typeof(MicrodotModule);
        }
    }
}
