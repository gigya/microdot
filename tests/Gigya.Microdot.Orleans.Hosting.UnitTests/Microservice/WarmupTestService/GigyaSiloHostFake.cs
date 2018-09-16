using System;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.SharedLogic.Configurations;
using NSubstitute;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class GigyaSiloHostFake : GigyaSiloHost
    {
        public GigyaSiloHostFake(IDependantClassFake dependantClassFake, WarmupTestServiceHostWithSiloHostFake host,  ILog log, OrleansConfigurationBuilder configBuilder, HttpServiceListener httpServiceListener, IEventPublisher<GrainCallEvent> eventPublisher, Func<LoadShedding> loadSheddingConfig, ISourceBlock<OrleansConfig> orleansConfigSourceBlock, OrleansConfig orleansConfig) : 
            base(log, configBuilder, httpServiceListener, eventPublisher, loadSheddingConfig, orleansConfigSourceBlock, orleansConfig)
        {
            dependantClassFake.Received(1).ThisClassIsWarmed();
            host.StopTest();
        }
    }
}
