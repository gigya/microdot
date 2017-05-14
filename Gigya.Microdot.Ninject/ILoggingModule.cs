using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Ninject.Syntax;

namespace Gigya.Microdot.Ninject
{
   public interface ILoggingModule
   {
      void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding);
   }
}