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
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
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
        public IKernel Kernel;

        private IRequestListener requestListener;

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

        protected override void OnStart()
        {
            Kernel = new StandardKernel(new NinjectSettings { ActivationCacheDisabled = true });

            var env = HostEnvironment.CreateDefaultEnvironment(ServiceName, InfraVersion, Arguments);
            Kernel.Bind<IEnvironment>().ToConstant(env).InSingletonScope();
            Kernel.Bind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();

            this.PreConfigure(Kernel, Arguments);
            this.Configure(Kernel);

            Kernel.Get<SystemInitializer.SystemInitializer>().Init();

            CrashHandler = Kernel.Get<ICrashHandler>();
            CrashHandler.Init(OnCrash);

            IWorkloadMetrics workloadMetrics = Kernel.Get<IWorkloadMetrics>();
            workloadMetrics.Init();

            var metricsInitializer = Kernel.Get<IMetricsInitializer>();
            metricsInitializer.Init();

            this.PreInitialize(Kernel);

            this.OnInitilize(Kernel);

            VerifyConfigurationsIfNeeded(Kernel.Get<MicrodotHostingConfig>(), Kernel.Get<ConfigurationVerificator>());

            this.Warmup(Kernel);

            //don't move up the get should be after all the binding are done
            var log = Kernel.Get<ILog>();

            this.requestListener = Kernel.Get<IRequestListener>();
            this.requestListener.Listen();

            log.Info(_ => _("start getting traffic", unencryptedTags: new { siloName = CurrentApplicationInfo.HostName }));
        }

        protected override void OnStop()
        {
            if (Arguments.ServiceDrainTimeSec.HasValue)
            {
                Kernel.Get<ServiceDrainController>().StartDrain();
                Thread.Sleep(Arguments.ServiceDrainTimeSec.Value * 1000);
            }
            Kernel.Get<SystemInitializer.SystemInitializer>().Dispose();
            Kernel.Get<IWorkloadMetrics>().Dispose();

            this.requestListener.Stop();

            try
            {
                Kernel.Get<ILog>().Info(x => x($"{ this.requestListener.GetType().Name } stopped gracefully, trying to dispose dependencies."));
            }
            catch
            {
                Console.WriteLine($"{ this.requestListener.GetType().Name } stopped gracefully, trying to dispose dependencies.");
            }

            Dispose();
        }

        /// <summary>
        /// An extensibility point - this method is called in process of configuration objects verification.
        /// </summary>
        protected override void OnVerifyConfiguration()
        {
            Kernel = new StandardKernel(new NinjectSettings { ActivationCacheDisabled = true });

            var env = HostEnvironment.CreateDefaultEnvironment(ServiceName, InfraVersion, Arguments);
            Kernel.Bind<IEnvironment>().ToConstant(env).InSingletonScope();
            Kernel.Bind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();

            Kernel.Load(
                new ConfigVerificationModule(
                    this.GetLoggingModule(),
                    Arguments,
                    env.ApplicationInfo.Name,
                    InfraVersion));

            VerifyConfiguration(Kernel.Get<ConfigurationVerificator>());
        }

        protected abstract ILoggingModule GetLoggingModule();

        /// <summary>
        /// Used to initialize service dependencies. This method is called before OnInitialize(), 
        /// and should include common behavior for a family of services. 
        /// When overridden on the family services base, it is recommended to mark it as sealed, 
        /// to prevent concrete services from overriding the common behavior. 
        /// </summary>
        /// <param name="kernel"></param>
        protected virtual void PreInitialize(IKernel kernel) { }

        /// <summary>
        /// Extensibility point - this method is called after the Kernel is configured and before service starts
        /// processing incoming request.
        /// </summary>
        /// <param name="resolutionRoot">Used to retrieve dependencies from Ninject.</param>
        protected virtual void OnInitilize(IKernel kernel) { }

        /// <summary>
        /// Used to configure Kernel in abstract base-classes, which should apply to any concrete service that inherits from it.
        /// Should be overridden when creating a base-class that should include common behaviour for a family of services, without
        /// worrying about concrete service authors forgetting to call base.Configure(). Nevertheless, when overriding this method,
        /// you should always call base.PreConfigure(), and if all inheritors of the class are concrete services, you should also
        /// mark this method as sealed to prevent confusion with Configure().
        /// </summary>
        /// <param name="kernel">Kernel that should contain bindings for the service.</param>
        protected virtual void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            kernel.Rebind<IActivator>().To<InstanceBasedActivator<TInterface>>().InSingletonScope();
            kernel.Rebind<IServiceInterfaceMapper>().To<IdentityServiceInterfaceMapper>().InSingletonScope().WithConstructorArgument(typeof(TInterface));

            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();

            kernel.Bind<IRequestListener>().To<HttpServiceListener>();

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

        protected void Configure(IKernel kernel)
        {
            this.Configure(kernel, kernel.Get<BaseCommonConfig>());
        }

        protected virtual void Warmup(IKernel kernel) { }
        protected override void Dispose(bool disposing)
        {
            if (!Kernel.IsDisposed && !disposing)
                SafeDispose(Kernel);

            base.Dispose(disposing);
        }
    }
}
