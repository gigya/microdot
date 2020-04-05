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
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.Configuration;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using Ninject;
using Ninject.Syntax;
using Orleans;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    /// <summary>
    /// Base class for all Orleans services that use Ninject. Override <see cref="Configure"/> to configure your own
    /// bindings and choose which infrastructure features you'd like to enable. Override
    /// <see cref="AfterOrleansStartup"/> to run initialization code after the silo starts.
    /// </summary>
    public abstract class MicrodotOrleansServiceHost : ServiceHostBase, IKernelConfigurator //ServiceHostBase
    {
        protected class OrleansConfigurator : IOrleansConfigurator
        {
            private readonly MicrodotOrleansServiceHost host;

            public OrleansConfigurator(MicrodotOrleansServiceHost host)
            {
                this.host = host ?? throw new ArgumentNullException(nameof(host));
            }

            public Task AfterOrleansStartup(IGrainFactory grainFactory)
            {
                return this.host.AfterOrleansStartup(grainFactory);
            }
        }

        public Microdot.Ninject.Host.Host Host { get; }

        public ServiceArguments Arguments => this.Host.Arguments;

        protected MicrodotOrleansServiceHost(HostConfiguration configuration)
        {
            this.Host = new Microdot.Ninject.Host.Host(
                configuration,
                this,
                new Version());

            this.Host.OnStopped += (s, a) => this.OnStop();
            this.Host.OnStarted += (s, a) => this.OnStart();
            this.Host.OnCrashed += (s, a) => this.OnCrash();
        }

        public override void Run(ServiceArguments argsOverride = null)
        {
            this.Host.Run(argsOverride);
        }

        public override Task WaitForServiceStartedAsync()
        {
            return this.Host.WaitForServiceStartedAsync();
        }

        public override void Stop()
        {
            this.Host.Stop();
        }

        public override Task<StopResult> WaitForServiceGracefullyStoppedAsync()
        {
            return this.Host.WaitForServiceGracefullyStoppedAsync();
        }

        public override void Dispose()
        {
            this.Host.Dispose();
        }

        public abstract ILoggingModule GetLoggingModule();


        /// <summary>
        /// Used to initialize service dependencies. This method is called before OnInitialize(), 
        /// and should include common behaviour for a family of services. 
        /// When overriden on the family services base, it is recommended to mark it as sealed, 
        /// to prevent concrete services from overriding the common behaviour. 
        /// </summary>
        /// <param name="kernel"></param>
        protected virtual void PreInitialize(IKernel kernel)
        {
            kernel.Get<SystemInitializer>().Init();
            
            // moved to Host
            //CrashHandler = kernel.Get<ICrashHandler>();
            //CrashHandler.Init(OnCrash);

            IWorkloadMetrics workloadMetrics = kernel.Get<IWorkloadMetrics>();
            workloadMetrics.Init();

            var metricsInitializer = kernel.Get<IMetricsInitializer>();
            metricsInitializer.Init();
        }

        /// <summary>
        /// Extensibility point - this method is called after the Kernel is configured and before service starts
        /// processing incoming request.
        /// </summary>
        /// <param name="resolutionRoot">Used to retrieve dependencies from Ninject.</param>
        protected virtual void OnInitilize(IKernel kernel)
        {

        }

        protected virtual void Warmup(IKernel kernel)
        {
            IWarmup warmup = kernel.Get<IWarmup>();
            warmup.Warmup();
        }

        /// <summary>
        /// Used to configure Kernel in abstract base-classes, which should apply to any concrete service that inherits from it.
        /// Should be overridden when creating a base-class that should include common behaviour for a family of services, without
        /// worrying about concrete service authors forgetting to call base.Configure(). Nevertheless, when overriding this method,
        /// you should always call base.PreConfigure(), and if all inheritors of the class are concrete services, you should also
        /// mark this method as sealed to prevent confusion with Configure().
        /// </summary>
        /// <param name="kernel"></param>
        protected virtual void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();
            kernel.Load<MicrodotOrleansHostModule>();
            kernel.Rebind<ServiceArguments>().ToConstant(Arguments);

            kernel.Bind<IOrleansConfigurator>().ToConstant(
                new OrleansConfigurator(this));

            kernel.Bind<IRequestListener>().To<GigyaSiloHost>();

            GetLoggingModule().Bind(kernel.Rebind<ILog>(), kernel.Rebind<IEventPublisher>(), kernel.Rebind<Func<string, ILog>>());

        }

        protected void Configure(IKernel kernel)
        {
            this.Configure(kernel, kernel.Get<OrleansCodeConfig>());
        }


        /// <summary>
        /// When overridden, allows a service to configure its Ninject bindings and infrastructure features. Called
        /// after infrastructure was binded but before the silo is started.
        /// </summary>
        /// <param name="kernel">A <see cref="IKernel"/> already configured with infrastructure bindings.</param>
        /// <param name="commonConfig">An <see cref="OrleansCodeConfig"/> that allows you to select which
        ///     infrastructure features you'd like to enable.</param>
        protected virtual void Configure(IKernel kernel, OrleansCodeConfig commonConfig) { }

        protected virtual void OnStop()
        {
        }

        protected virtual void OnCrash()
        {
        }

        protected virtual void OnStart()
        {
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        /// <summary>
        /// When overridden, allows running startup code after the silo has started, i.e. IBootstrapProvider.Init().
        /// </summary>
        /// <param name="grainFactory">A <see cref="GrainFactory"/> used to access grains.</param>        
        protected virtual async Task AfterOrleansStartup(IGrainFactory grainFactory) { }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        void IKernelConfigurator.Configure(IKernel kernel)
        {
            this.Configure(kernel);
        }

        ILoggingModule IKernelConfigurator.GetLoggingModule()
        {
            return this.GetLoggingModule();
        }

        void IKernelConfigurator.OnInitilize(IKernel kernel)
        {
            this.OnInitilize(kernel);
        }

        void IKernelConfigurator.PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            this.PreConfigure(kernel, Arguments);
        }

        void IKernelConfigurator.PreInitialize(IKernel kernel)
        {
            this.PreInitialize(kernel);
        }

        void IKernelConfigurator.Warmup(IKernel kernel)
        {
            this.Warmup(kernel);
        }
    }
}
