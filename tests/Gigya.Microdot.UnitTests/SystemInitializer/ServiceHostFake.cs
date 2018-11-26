using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Syntax;
using NSubstitute;

namespace Gigya.Microdot.UnitTests.SystemInitializer
{
    [HttpService(12328)]
    public interface IServiceFake
    {
        Task<int> Add(int a, int b);
    }

    public class ServiceHostFake<TFake> : MicrodotServiceHost<IServiceFake> 
    {
        private TFake _fake;
        private TaskCompletionSource<bool> _hostInitializedEvent = new TaskCompletionSource<bool>();
        public ServiceHostFake(TFake fake)
        {
            _fake = fake;
        }

        protected override ILoggingModule GetLoggingModule()
        {
            return new NLogModule();
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Rebind<TFake>().ToConstant(_fake);
            kernel.Rebind<IEventPublisher<CrashEvent>>().ToConstant(Substitute.For<IEventPublisher<CrashEvent>>());
            kernel.Rebind<IMetricsInitializer>().ToConstant(Substitute.For<IMetricsInitializer>());
        }

        protected override void OnInitilize(IResolutionRoot resolutionRoot)
        {
            _hostInitializedEvent.SetResult(true);
        }

        public async Task StopHost()
        {
            await WaitForServiceStartedAsync();
            Stop();
            await WaitForServiceGracefullyStoppedAsync();
            Dispose();
        }

        public async Task WaitForHostInitialized()
        {
            await _hostInitializedEvent.Task;
        }
    }
}
