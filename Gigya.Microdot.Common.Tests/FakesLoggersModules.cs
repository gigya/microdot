using System;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Common.Tests
{
    public class FakesLoggersModules : ILoggingModule
    {
        public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding, IBindingToSyntax<Func<string, ILog>> logFactory)
        {
            logBinding.To<ConsoleLog>().InSingletonScope();
            logFactory.ToMethod(c => caller => c.Kernel.Get<ConsoleLog>()).InSingletonScope();
            eventPublisherBinding.To<SpyEventPublisher>().InSingletonScope();
        }
    }
}