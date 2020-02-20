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
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using Gigya.Microdot.SharedLogic.SystemWrappers;
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

        private KernelConfigurator<TInterface> kernelConfigurator;


        /// <summary>
        /// Creates a new instance of <see cref="MicrodotServiceHost{TInterface}"/>
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the provided type provided for the <typeparamref name="TInterface" />
        /// generic argument is not an interface.</exception>
        protected MicrodotServiceHost(HostConfiguration configuration, KernelConfigurator<TInterface> kernelConfigurator) : base(configuration)
        {
            this.kernelConfigurator = kernelConfigurator ?? throw new ArgumentNullException(nameof(kernelConfigurator));
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
            
            Kernel.Bind<IEnvironment>().ToConstant(HostConfiguration).InSingletonScope();
            Kernel.Bind<CurrentApplicationInfo>().ToConstant(HostConfiguration.ApplicationInfo).InSingletonScope();

            this.kernelConfigurator.PreConfigure(Kernel, Arguments);
            this.kernelConfigurator.Configure(Kernel);

            this.kernelConfigurator.PreInitialize(Kernel);

            CrashHandler = Kernel.Get<ICrashHandler>();
            CrashHandler.Init(OnCrash);

            this.kernelConfigurator.OnInitilize(Kernel);
            //don't move up the get should be after all the binding are done
            var log = Kernel.Get<ILog>();
            Listener = Kernel.Get<HttpServiceListener>();
            Listener.Start();
           
            Listener.StartGettingTraffic();
            log.Info(_ => _("start getting traffic", unencryptedTags: new { siloName = HostConfiguration.ApplicationInfo.HostName }));
        }
        
        protected override void OnVerifyConfiguration()
        {
            Kernel = CreateKernel();
            Kernel.Bind<IEnvironment>().ToConstant(HostConfiguration).InSingletonScope();
            Kernel.Bind<CurrentApplicationInfo>().ToConstant(HostConfiguration.ApplicationInfo).InSingletonScope();
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
