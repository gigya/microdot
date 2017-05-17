using System;

using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic;

using Ninject;
using Ninject.Syntax;

namespace Gigya.Ninject.Host
{
    /// <summary>
    /// Base class for all non-Orleans services that use Ninject. Override <see cref="Configure"/> to configure your own
    /// bindings and choose which infrastructure features you'd like to enable. 
    /// </summary>
    /// <typeparam name="TInterface">The interface of the service.</typeparam>
    public abstract class MicrodotServiceHost<TInterface> : GigyaServiceHost
    {
        private bool disposed = false;

        private IKernel Kernel { get; set; }

        private HttpServiceListener Listener { get; set; }


        /// <summary>
        /// Creates a new instance of <see cref="MicrodotServiceHost{TInterface}"/>
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the provided type provided for the <see cref="TInterface"/>
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

            PreConfigure();

            Kernel.Rebind<ServiceArguments>().ToConstant(Arguments);
            Kernel.Rebind<IActivator>().To<InstanceBasedActivator<TInterface>>().InSingletonScope();
            Kernel.Rebind<IServiceInterfaceMapper>().To<IdentityServiceInterfaceMapper>().InSingletonScope().WithConstructorArgument(typeof(TInterface));

            Configure(Kernel, Kernel.Get<BaseCommonConfig>());

            OnInitilize(Kernel);

            Listener = Kernel.Get<HttpServiceListener>();
            Listener.Start();
        }

        /// <summary>
        /// Used to configure Kernel, shoud be overriden only if you want to prepare a base service configured for your common usecase
        /// in this case you shoud call base.Preconfigure before your own code.
        /// </summary>
        protected virtual void PreConfigure()
        {
            Kernel.Load<MicrodotModule>();
            Kernel.Load<MicrodotHostingModule>();
            GetLoggingModule().Bind(Kernel.Rebind<ILog>(), Kernel.Rebind<IEventPublisher>());
        }

        /// <summary>
        /// Extensability point, this method is called after kernel is configured and before, service is willing to 
        /// serve incoming request.
        /// </summary>
        /// <param name="resolutionRoot"></param>
        protected virtual void OnInitilize(IResolutionRoot resolutionRoot)
        {
            
        }

        /// <summary>
        /// Creates the <see cref="IKernel"/> used by this instance. Defaults to using <see cref="StandardKernel"/>, but
        /// can be overridden to customize which kernel is used (e.g. MockingKernel);
        /// </summary>
        /// <returns>The kernel to use.</returns>
        protected virtual IKernel CreateKernel()
        {
            return new StandardKernel();
        }


        protected override void Dispose(bool disposing)
        {
            if(disposed)
                return;
            Kernel?.Dispose();
            disposed = true;
            base.Dispose(disposing);
        }


        /// <summary>
        /// When overridden, allows a service to configure its Ninject bindings and infrastructure features. Called
        /// after infrastructure was binded but before the silo is started. You must bind an implementation to the
        /// interface defined by <see cref="TInterface"/>.
        /// </summary>
        /// <param name="kernel">A <see cref="IKernel"/> already configured with infrastructure bindings.</param>
        /// <param name="commonConfig">An <see cref="BaseCommonConfig"/> that allows you to select which
        ///     infrastructure features you'd like to enable.</param>
        protected abstract void Configure(IKernel kernel, BaseCommonConfig commonConfig);


        /// <summary>
        /// Called when the service stops. This methods stops the silo. In most scenarios, you shouldn't override this
        /// method.
        /// </summary>
        protected override void OnStop()
        {
            Listener.Dispose();
        }
    }
}
