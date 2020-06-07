using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.Orleans.Ninject.Host;
using Ninject;
using Ninject.Syntax;
using Orleans;
using Orleans.Hosting;
using Orleans.Providers;
using System.Threading.Tasks;
using static Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService.CalculatorServiceHost;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.StorageProviderTest
{
    public class MemoryServiceHost : MicrodotOrleansServiceHost
    {
        public string ServiceName => nameof(MemoryServiceHost);

        public override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
        }
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            base.Configure(kernel, commonConfig);
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();

            kernel.RebindForTests();
        }
        public const string MemoryStorageProvider = "MemoryStorageProvider";

        public MemoryServiceHost() : base(
            new HostEnvironment(
                    new TestHostEnvironmentSource(appName: "IMemoryService")), 
            new System.Version())
        {
        }

        protected override void OnInitilize(IKernel kerenl)
       {
            base.OnInitilize(kerenl);
            var siloHostBuilder = kerenl.Get<OrleansConfigurationBuilder>().GetBuilder();

            siloHostBuilder.AddMemoryGrainStorage(MemoryStorageProvider,
                options => options.NumStorageGrains = 10);
        }
    }
}