using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
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
            if (_useHttpLog)
                logBinding.To<HttpLog>();
            else
                logBinding.To<ConsoleLog>();

            eventPublisherBinding.To<NullEventPublisher>();
        }
    }
}