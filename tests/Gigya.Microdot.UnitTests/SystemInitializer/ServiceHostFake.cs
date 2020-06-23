using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.HttpService;
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
        public override string ServiceName => nameof(IServiceFake).Substring(1);

        private TFake _fake;
        private readonly HostEnvironment environment;

        public ServiceHostFake(TFake fake, HostEnvironment environment)
        {
            _fake = fake;
            this.environment = environment;
        }

        protected override ILoggingModule GetLoggingModule()
        {
            return new NLogModule();
        }

        protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            kernel.Rebind<IEnvironment>().ToConstant(environment).InSingletonScope();
            kernel.Rebind<CurrentApplicationInfo>().ToConstant(environment.ApplicationInfo).InSingletonScope();

            base.PreConfigure(kernel, Arguments);
       
            kernel.Rebind<TFake>().ToConstant(_fake);
            kernel.Rebind<IEventPublisher<CrashEvent>>().ToConstant(Substitute.For<IEventPublisher<CrashEvent>>());
            kernel.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();
            kernel.Rebind<IMetricsInitializer>().ToConstant(Substitute.For<IMetricsInitializer>());
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
          //  throw new System.NotImplementedException();
        }
    }
}
