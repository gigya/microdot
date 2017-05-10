using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.SharedLogic;

using Ninject.Modules;

using Orleans;
using Orleans.Runtime.Configuration;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    /// <summary>
    /// Binding needed for Orleans based host
    /// </summary>
    public class MicrodotOrleansHostModule : NinjectModule
    {
        public override void Load()
        {
            this.BindClassesAsSingleton(new[] { typeof(Grain) }, typeof(OrleansHostingAssembly));
            this.BindInterfacesAsSingleton(new[] {typeof(Grain)}, typeof(OrleansHostingAssembly));
            
            Rebind<IActivator>().To<GrainActivator>().InSingletonScope();
            Rebind<IWorker>().To<ProcessingGrainWorker>().InSingletonScope();
            Rebind<IServiceInterfaceMapper>().To<OrleansServiceInterfaceMapper>().InSingletonScope();
            Rebind<ClusterConfiguration>().ToSelf().InSingletonScope();

            Rebind<BaseCommonConfig, OrleansCodeConfig>().To<OrleansCodeConfig>().InSingletonScope();                                    
        }
    }
}
