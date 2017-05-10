using Gigya.Microdot.Hosting;

using Ninject.Modules;

namespace Gigya.Microdot.Ninject
{
    /// <summary>
    /// Contains binding needed for hosting layer
    /// </summary>
    public class MicrodotHostingModule : NinjectModule
    {
        public override void Load()
        {
            this.BindClassesAsSingleton(assemblies: new[] { typeof(HostingAssembly) });
            this.BindInterfacesAsSingleton(assemblies: new[] { typeof(HostingAssembly) });
        }
    }
}