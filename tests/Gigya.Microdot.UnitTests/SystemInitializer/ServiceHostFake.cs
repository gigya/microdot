using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Ninject;
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
        public string ServiceName => nameof(IServiceFake).Substring(1);

        private TFake _fake;
        public ServiceHostFake(TFake fake, HostConfiguration configuration)
            : base(configuration)
        {
            _fake = fake;
        }

        public override ILoggingModule GetLoggingModule()
        {
            return new NLogModule();
        }

        public override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            base.PreConfigure(kernel, Arguments);
       
            kernel.Rebind<TFake>().ToConstant(_fake);
            kernel.Rebind<IEventPublisher<CrashEvent>>().ToConstant(Substitute.For<IEventPublisher<CrashEvent>>());
            kernel.Rebind<IMetricsInitializer>().ToConstant(Substitute.For<IMetricsInitializer>());
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
          //  throw new System.NotImplementedException();
        }
    }
}
