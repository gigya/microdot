using System;

using Gigya.Microdot.Configuration;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic;

using Metrics;

using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Factory;
using Ninject.Modules;

namespace Gigya.Microdot.Ninject
{
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
            if (Kernel.CanResolve<Func<long, DateTime>>() == false)
                Kernel.Load<FuncModule>();

            this.BindClassesAsSingleton(NonSingletonBaseTypes, typeof(ConfigurationAssembly), typeof(ServiceProxyAssembly));
            this.BindInterfacesAsSingleton(NonSingletonBaseTypes, typeof(ConfigurationAssembly), typeof(ServiceProxyAssembly), typeof(SharedLogicAssembly),typeof(ServiceDiscoveryAssembly));

            Bind<IRemoteHostPoolFactory>().ToFactory();

            Kernel.BindPerKey<string, ReachabilityChecker, IServiceDiscovery, ServiceDiscovery.ServiceDiscovery>();
            Kernel.BindPerString<IServiceProxyProvider, ServiceProxyProvider>();

            Rebind<MetricsContext>()
                .ToMethod(c => Metric.Context(GetTypeOfTarget(c).Name))
                .InScope(GetTypeOfTarget);

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
