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
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Ninject.Host
{
    /// <summary>
    /// Base class for all non-Orleans services that use Ninject. Override <see cref="Configure"/> to configure your own
    /// bindings and choose which infrastructure features you'd like to enable. 
    /// </summary>
    /// <typeparam name="TInterface">The interface of the service.</typeparam>
    public abstract class MicrodotServiceHost<TInterface> : ServiceHostBase
    {
        private bool disposed;

        private readonly object disposeLockHandle = new object();

        private IKernel Kernel { get; set; }

        private HttpServiceListener Listener { get; set; }


        /// <summary>
        /// Creates a new instance of <see cref="MicrodotServiceHost{TInterface}"/>
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the provided type provided for the <typeparamref name="TInterface" />
        /// generic argument is not an interface.</exception>
        protected MicrodotServiceHost()
        {
            if (typeof(TInterface).IsInterface == false)
                throw new ArgumentException($"The specified type provided for the {nameof(TInterface)} generic argument must be an interface.");
        }

        protected abstract ILoggingModule GetLoggingModule();

        /// <summary>
        /// Called when the service is started. This method first calls <see cref="CreateKernel"/>, configures it with
        /// infrastructure binding, calls <see cref="Configure"/> to configure additional bindings and settings, then
        /// start a <see cref="HttpServiceListener"/>. In most scenarios, you shouldn't override this method.
        /// </summary>
        protected override void OnStart()
        {
            Kernel = CreateKernel();
            
            Kernel.Bind<CurrentApplicationInfo>().ToConstant(new CurrentApplicationInfo(ServiceName, Arguments.InstanceName)).InSingletonScope();
            Kernel.Rebind<IActivator>().To<InstanceBasedActivator<TInterface>>().InSingletonScope();
            Kernel.Rebind<IServiceInterfaceMapper>().To<IdentityServiceInterfaceMapper>().InSingletonScope().WithConstructorArgument(typeof(TInterface));

            PreConfigure(Kernel);
            Configure(Kernel, Kernel.Get<BaseCommonConfig>());

            PreInitialize(Kernel);
            OnInitilize(Kernel);

            Listener = Kernel.Get<HttpServiceListener>();
            Listener.Start();
        }

        /// <summary>
        /// Used to initialize service dependencies. This method is called before OnInitialize(), 
        /// and should include common behavior for a family of services. 
        /// When overridden on the family services base, it is recommended to mark it as sealed, 
        /// to prevent concrete services from overriding the common behavior. 
        /// </summary>
        /// <param name="kernel"></param>
        protected virtual void PreInitialize(IKernel kernel)
        {
            Kernel.Get<SystemInitializer.SystemInitializer>().Init();
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
        /// <param name="kernel">Kernel that should contain bindings for the service.</param>
        protected virtual void PreConfigure(IKernel kernel)
        {
            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();
            GetLoggingModule().Bind(kernel.Rebind<ILog>(), kernel.Rebind<IEventPublisher>(), kernel.Rebind<Func<string, ILog>>());
            kernel.Rebind<ServiceArguments>().ToConstant(Arguments).InSingletonScope();
        }

        /// <summary>
        /// When overridden, allows a service to configure its Ninject bindings and infrastructure features. Called
        /// after infrastructure was binded but before the silo is started. You must bind an implementation to the
        /// interface defined by <typeparamref name="TInterface" />.
        /// </summary>
        /// <param name="kernel">A <see cref="IKernel"/> already configured with infrastructure bindings.</param>
        /// <param name="commonConfig">An <see cref="BaseCommonConfig"/> that allows you to select which
        /// infrastructure features you'd like to enable.</param>
        protected abstract void Configure(IKernel kernel, BaseCommonConfig commonConfig);



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
            Kernel.Get<SystemInitializer.SystemInitializer>().Dispose();
            Kernel.Get<IWorkloadMetrics>().Dispose();
            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            lock (disposeLockHandle)
            {
                try
                {
                    if (disposed)
                        return;

                    SafeDispose(Listener);

                    if (!Kernel.IsDisposed)
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
