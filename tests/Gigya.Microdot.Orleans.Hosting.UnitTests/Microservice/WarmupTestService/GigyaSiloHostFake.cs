using System;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.SharedLogic.Configurations;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class GigyaSiloHostFake : GigyaSiloHost
    {
        public GigyaSiloHostFake(WarmupTestServiceHostWithSiloHostFake host,  ILog log, OrleansConfigurationBuilder configBuilder,
            HttpServiceListener httpServiceListener, IEventPublisher<GrainCallEvent> eventPublisher, Func<LoadShedding> loadSheddingConfig,
            ISourceBlock<OrleansConfig> orleansConfigSourceBlock, OrleansConfig orleansConfig, Func<GrainLogging> grainLoggingConfig) : 
            base(log, configBuilder, httpServiceListener, eventPublisher, loadSheddingConfig, orleansConfigSourceBlock, orleansConfig, grainLoggingConfig)
        {
            try
            {
                Assert.AreEqual(DependantClassFake.WarmedTimes, 1);
            }
            catch (Exception e)
            {

            }
            finally
            {
                host.StopHost(); //awaitable, but can't be awaited in ctor. There is await into the "StopHost" method
            }
        }
    }
}
