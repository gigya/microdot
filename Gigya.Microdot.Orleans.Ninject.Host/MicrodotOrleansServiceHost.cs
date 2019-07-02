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
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
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
    public abstract class MicrodotOrleansServiceHost : ServiceHostBase
    {
        private bool disposed;

        protected GigyaSiloHost SiloHost { get; set; }

        private IKernel Kernel { get; set; }

        public abstract ILoggingModule GetLoggingModule();


        /// <summary>
        /// Called when the service is started. This method first calls <see cref="CreateKernel"/>, configures it with
        /// infrastructure binding, calls <see cref="Configure"/> to configure additional bindings and settings, then
        /// start silo using <see cref="GigyaSiloHost"/>. In most scenarios, you shouldn't override this method.
        /// </summary>
        protected override void OnStart()
        {
            Kernel = CreateKernel();

            Kernel.Bind<CurrentApplicationInfo>().ToConstant(new CurrentApplicationInfo(ServiceName, Arguments.InstanceName, InfraVersion)).InSingletonScope();

            PreConfigure(Kernel);

            Configure(Kernel, Kernel.Get<OrleansCodeConfig>());

            PreInitialize(Kernel);

            OnInitilize(Kernel);

            Warmup(Kernel);

            SiloHost = Kernel.Get<GigyaSiloHost>();
            SiloHost.Start(Arguments, AfterOrleansStartup);
        }

        /// <summary>
        /// Used to initialize service dependencies. This method is called before OnInitialize(), 
        /// and should include common behaviour for a family of services. 
        /// When overriden on the family services base, it is recommended to mark it as sealed, 
        /// to prevent concrete services from overriding the common behaviour. 
        /// </summary>
        /// <param name="kernel"></param>
        protected virtual void PreInitialize(IKernel kernel)
        {
            Kernel.Get<SystemInitializer>().Init();
            CrashHandler = kernel.Get<ICrashHandler>();
            CrashHandler.Init(OnCrash);

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
        protected virtual void OnInitilize(IResolutionRoot resolutionRoot)
        {

        }

        protected virtual void Warmup(IKernel kernel)
        {
            IWarmup warmup = kernel.Get<IWarmup>();
            warmup.Warmup();
        }

        protected override void OnVerifyConfiguration()
        {
            Kernel = CreateKernel();
            Kernel.Load(new ConfigVerificationModule(GetLoggingModule(), Arguments, ServiceName, InfraVersion));
            ConfigurationVerificator = Kernel.Get<Configuration.ConfigurationVerificator>();
            base.OnVerifyConfiguration();
        }

        /// <summary>
        /// Creates the <see cref="IKernel"/> used by this instance. Defaults to using <see cref="StandardKernel"/>, but
        /// can be overridden to customize which kernel is used (e.g. MockingKernel);
        /// </summary>
        /// <returns>The kernel to use.</returns>
        protected virtual IKernel CreateKernel()
        {
            return new StandardKernel(new NinjectSettings { ActivationCacheDisabled = true });


        }


        /// <summary>
        /// Used to configure Kernel in abstract base-classes, which should apply to any concrete service that inherits from it.
        /// Should be overridden when creating a base-class that should include common behaviour for a family of services, without
        /// worrying about concrete service authors forgetting to call base.Configure(). Nevertheless, when overriding this method,
        /// you should always call base.PreConfigure(), and if all inheritors of the class are concrete services, you should also
        /// mark this method as sealed to prevent confusion with Configure().
        /// </summary>
        /// <param name="kernel"></param>
        protected virtual void PreConfigure(IKernel kernel)
        {
            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();
            kernel.Load<MicrodotOrleansHostModule>();
            kernel.Rebind<ServiceArguments>().ToConstant(Arguments);
            GetLoggingModule().Bind(kernel.Rebind<ILog>(), kernel.Rebind<IEventPublisher>(), kernel.Rebind<Func<string, ILog>>());

        }


        /// <summary>
        /// When overridden, allows a service to configure its Ninject bindings and infrastructure features. Called
        /// after infrastructure was binded but before the silo is started.
        /// </summary>
        /// <param name="kernel">A <see cref="IKernel"/> already configured with infrastructure bindings.</param>
        /// <param name="commonConfig">An <see cref="OrleansCodeConfig"/> that allows you to select which
        ///     infrastructure features you'd like to enable.</param>
        protected virtual void Configure(IKernel kernel, OrleansCodeConfig commonConfig) { }



#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        /// <summary>
        /// When overridden, allows running startup code after the silo has started, i.e. IBootstrapProvider.Init().
        /// </summary>
        /// <param name="grainFactory">A <see cref="GrainFactory"/> used to access grains.</param>        
        protected virtual async Task AfterOrleansStartup(IGrainFactory grainFactory) { }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        /// <summary>
        /// Called when the service stops. This methods stops the silo. In most scenarios, you shouldn't override this
        /// method.
        /// </summary>        
        protected override void OnStop()
        {
            if (Arguments.ServiceDrainTimeSec.HasValue)
            {
                Kernel.Get<ServiceDrainController>().StartDrain();
                Thread.Sleep(Arguments.ServiceDrainTimeSec.Value * 1000);
            }

            Kernel.Get<SystemInitializer>().Dispose();
            Kernel.Get<IWorkloadMetrics>().Dispose();
            SiloHost.Stop(); // This calls BeforeOrleansShutdown()
           
            try
            {
                Kernel.Get<ILog>().Info(x => x("Silo stopped gracefully, trying to dispose dependencies."));
            }
            catch
            {
                Console.WriteLine("Silo stopped gracefully, trying to dispose dependencies.");
            }

            Dispose();
        }

        private readonly object lockHandale = new object();
        protected override void Dispose(bool disposing)
        {
            lock (lockHandale)
            {
                try
                {
                    if (disposed)
                        return;

                    disposed = true;

                    if (!Kernel.IsDisposed && !disposing)
                        SafeDispose(Kernel);

                    base.Dispose(disposing);
                }
                finally
                {
                    disposed = true;
                }
            }
        }
    }
}
