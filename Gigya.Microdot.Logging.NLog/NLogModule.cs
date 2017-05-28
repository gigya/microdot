using System;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Ninject.Activation;
using Ninject.Syntax;

namespace Gigya.Microdot.Logging.NLog
{
    /// <summary>
    /// Configures the logger to be <see cref="NLogLogger"/> (instance per type) and the event publisher to be
    /// <see cref="LogEventPublisher"/>.
    /// </summary>
   public class NLogModule : ILoggingModule
   {        
      public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding)
      {
         logBinding
            .To<NLogLogger>()
            .InScope(GetTypeOfTarget)
            .WithConstructorArgument("receivingType", (context, target) => GetTypeOfTarget(context));

         eventPublisherBinding
            .To<LogEventPublisher>()
            .InSingletonScope();
      }

      private static Type GetTypeOfTarget(IContext context)
      {
         var type = context.Request.Target?.Member.DeclaringType;
         return type ?? typeof(NLogModule);
      }
   }
}