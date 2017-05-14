using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Ninject.Host;

using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{
    public class FakesLoggersModules : ILoggingModule
    {
        private readonly bool _useHttpLog;

        public FakesLoggersModules(bool useHttpLog)
        {
            _useHttpLog = useHttpLog;
        }

        public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding)
        {
            if(_useHttpLog)
                logBinding.To<HttpLog>();
            else
                logBinding.To<ConsoleLog>();

            eventPublisherBinding.To<NullEventPublisher>();
        }
    }

    public class CalculatorServiceHost : MicrodotOrleansServiceHost
    {
        private ILoggingModule LoggingModule { get; }

        public CalculatorServiceHost() : this(true)
        { }


        public CalculatorServiceHost(bool useHttpLog)
        {            
            LoggingModule = new FakesLoggersModules(useHttpLog);
        }


        protected override string ServiceName => "TestService";


        public override ILoggingModule GetLoggingModule()
        {
            return LoggingModule;
        }

        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {

        }
    }
}