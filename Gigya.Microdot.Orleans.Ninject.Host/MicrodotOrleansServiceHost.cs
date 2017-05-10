using System;
using System.Threading.Tasks;

using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.SharedLogic;

using Ninject;
using Ninject.Activation;

using Orleans;
using Orleans.Runtime.Configuration;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    /// <summary>
    /// Base class for all Orleans services that use Ninject. Override <see cref="Configure"/> to configure your own
    /// bindings and choose which infrastructure features you'd like to enable. Override
    /// <see cref="AfterOrleansStartup"/> to run initialization code after the silo starts.
    /// </summary>
    public abstract class MicrodotOrleansServiceHost : GigyaServiceHost
    {
        private bool disposed = false;

        protected GigyaSiloHost SiloHost { get; set; }

        /// <summary>
        /// Contains an instance of <see cref="ILog"/> that was configured by <see cref="Configure"/>. This property
        /// is populated only after <see cref="Configure"/> completes.
        /// </summary>
        protected ILog Log { get; set; }
        
        protected IKernel Kernel { get; set; }


        /// <summary>
        /// Called when the service is started. This method first calls <see cref="CreateKernel"/>, configures it with
        /// infrastructure binding, calls <see cref="Configure"/> to configure additional bindings and settings, then
        /// start silo using <see cref="GigyaSiloHost"/>. In most scenarios, you shouldn't override this method.
        /// </summary>
        protected override void OnStart()
        {
            var kernel = CreateKernel();
            Kernel = kernel;
            kernel.Load<MicrodotModule>();
            kernel.Load<MicrodotHostingModule>();
            kernel.Load<MicrodotOrleansHostModule>();

            kernel.Rebind<ILog>()
                .To<NlogBasedLogger>()
                .InScope(GetTypeOfTarget)
                .WithConstructorArgument("receivingType", (context, target) => GetTypeOfTarget(context));

            kernel.Rebind<IEventPublisher>()
                .To<LogBasedEventPublisher>()
                .InSingletonScope();

            kernel.Rebind<ServiceArguments>().ToConstant(Arguments);
            
            Configure(kernel, kernel.Get<OrleansCodeConfig>());

            Log = kernel.Get<ILog>();
            
            kernel.Get<ClusterConfiguration>().WithNinject(kernel);

            SiloHost = kernel.Get<GigyaSiloHost>();            
            SiloHost.Start(InfraOneTimeInits, BeforeOrleansShutdown);
        }


        private static Type GetTypeOfTarget(IContext context)
        {
            var type = context.Request.Target?.Member.DeclaringType;
            return type ?? typeof(MicrodotOrleansServiceHost);
        }


        private async Task InfraOneTimeInits(IGrainFactory factory)
        {
            await AfterOrleansStartup(factory);            
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


        /// <summary>
        /// When overridden, allows running shutdown code before the silo has stopped, i.e. IBootstrapProvider.Close().
        /// </summary>
        /// <param name="grainFactory">A <see cref="GrainFactory"/> used to access grains.</param>
        protected virtual async Task BeforeOrleansShutdown(IGrainFactory grainFactory) { }
        
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        /// <summary>
        /// Called when the service stops. This methods stops the silo. In most scenarios, you shouldn't override this
        /// method.
        /// </summary>
        protected override void OnStop()
        {
            SiloHost.Stop(); // This calls BeforeOrleansShutdown()
        }


        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;
            Kernel?.Dispose();
            disposed = true;
            base.Dispose(disposing);
        }
    }
}
