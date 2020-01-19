using System;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Ninject;

namespace Gigya.Microdot.Ninject
{
    public class MicrodotInitializer : IDisposable
    {
        public MicrodotInitializer(HostConfiguration hostConfiguration, ILoggingModule loggingModule, Action<IKernel> additionalBindings = null)
        {
            Kernel = new StandardKernel();
            Kernel.Load<MicrodotModule>();

            Kernel
                .Bind<IEnvironment>()
                .ToConstant(hostConfiguration)
                .InSingletonScope();

            Kernel
                .Bind<CurrentApplicationInfo>()
                .ToConstant(hostConfiguration.ApplicationInfo)
                .InSingletonScope();

            loggingModule.Bind(Kernel.Rebind<ILog>(), Kernel.Rebind<IEventPublisher>(), Kernel.Rebind<Func<string, ILog>>());
            // Set custom Binding 
            additionalBindings?.Invoke(Kernel);

            Kernel.Get<SystemInitializer.SystemInitializer>().Init();
        }

        public IKernel Kernel { get; private set; }

        public void Dispose()
        {
            Kernel?.Dispose();
        }

    }
}