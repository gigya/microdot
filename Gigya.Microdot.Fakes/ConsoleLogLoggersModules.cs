using System;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class ConsoleLogLoggersModules : ILoggingModule
    {
        public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding, IBindingToSyntax<Func<string, ILog>> logFactory)
        {
            logBinding.To<ConsoleLog>();

            logFactory.ToMethod(c => caller => c.Kernel.Get<ConsoleLog>());
            eventPublisherBinding.To<NullEventPublisher>();
        }
    }
}