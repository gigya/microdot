using System;
using System.Collections.Generic;
using System.Linq;

using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;

using Ninject;

using NSubstitute;

namespace Gigya.Microdot.Testing
{
   
    public class TestingKernel<T>: StandardKernel where T : ILog, new()
    {
        public const string APPNAME = "InfraTests";

        public TestingKernel(Action<IKernel> additionalBindings = null, Dictionary<string, string> mockConfig = null)
        {            
            CurrentApplicationInfo.Init(APPNAME);
          
            
            this.Load<MicrodotModule>();

            Rebind<IEventPublisher>().To<NullEventPublisher>();
            Rebind<ILog>().To<T>().InSingletonScope();
            Rebind<IDiscoverySourceLoader>().To<AlwaysLocalHost>().InSingletonScope();
            var locationsParserMock = Substitute.For<IConfigurationLocationsParser>();
            locationsParserMock.ConfigFileDeclarations.Returns(Enumerable.Empty<ConfigFileDeclaration>().ToArray());
            Rebind<IConfigurationLocationsParser>().ToConstant(locationsParserMock);

            additionalBindings?.Invoke(this);

            Rebind<IConfigurationDataWatcher, ManualConfigurationEvents>()
                  .To<ManualConfigurationEvents>()
                  .InSingletonScope();

            Rebind<IConfigItemsSource, OverridableConfigItems>()
                  .To<OverridableConfigItems>()
                  .InSingletonScope()
                  .WithConstructorArgument("data", mockConfig ?? new Dictionary<string, string>());



            Rebind<IHealthMonitor>().To<FakeHealthMonitor>().InSingletonScope();
        }

        public OverridableConfigItems GetConfigOverride()
        {
            return this.Get<OverridableConfigItems>();
        }
       
        public void RaiseConfigChangeEvent()
        {
            this.Get<ManualConfigurationEvents>().RaiseChangeEvent();
        }
    }
}