using System.Diagnostics;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Ninject.Host;

using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{
    public class CalculatorServiceHost : MicrodotOrleansServiceHost
    {
        private bool UseHttpLog { get; }


        public CalculatorServiceHost() : this(true)
        { }


        public CalculatorServiceHost(bool useHttpLog)
        {
            UseHttpLog = useHttpLog;
        }


        protected override string ServiceName => "TestService";


        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            if (UseHttpLog)
                BindFakeLog<HttpLog>(kernel);
            else
                BindFakeLog<ConsoleLog>(kernel);

           // kernel.Rebind<IMetricsInitializer>().To<TestingMetricsInitializer>();
        }


        private void BindFakeLog<T>(IKernel kernel) where T : FakeLog, new()
        {
            kernel.Rebind<ILog>().ToConstant(new T { MinimumTraceLevel = TraceEventType.Warning });
            //kernel.Bind<ILog>().ToConstant(new T { MinimumTraceLevel = TraceEventType.Information }).WhenInjectedInto<NonServiceGrain>();
        }
    }
}