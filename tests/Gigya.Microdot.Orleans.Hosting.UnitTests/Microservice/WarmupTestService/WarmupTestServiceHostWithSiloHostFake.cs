using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.SharedLogic;
using Ninject;
using NSubstitute;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class WarmupTestServiceHostWithSiloHostFake : CalculatorServiceHost
    {
        private IDependantClassFake _dependantClassFake = Substitute.For<IDependantClassFake>();
        private TaskCompletionSource<bool> _hostDisposedEvent = new TaskCompletionSource<bool>();

        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>();

            kernel.Rebind<GigyaSiloHost>().To<GigyaSiloHostFake>();
            kernel.Rebind<IDependantClassFake>().ToConstant(_dependantClassFake);
            kernel.Rebind<ILog>().To<NullLog>();
            kernel.Rebind<IServiceInterfaceMapper>().To<OrleansServiceInterfaceMapper>();
            kernel.Rebind<IAssemblyProvider>().To<AssemblyProvider>();

            ServiceArguments args = new ServiceArguments(basePortOverride:9555);
            kernel.Rebind<ServiceArguments>().ToConstant(args);
            kernel.Rebind<WarmupTestServiceHostWithSiloHostFake>().ToConstant(this);
        }

        public async Task StopHost()
        {
            //await WaitForServiceStartedAsync();
            Stop();
            //await WaitForServiceGracefullyStoppedAsync();
            //Dispose();

            _hostDisposedEvent.SetResult(true);
        }

        public async Task WaitForHostDisposed()
        {
            await _hostDisposedEvent.Task;
        }
    }
}
