using System;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Ninject.Activation;
using Ninject.Syntax;

namespace Gigya.Microdot.Logging
{
   public class SampleLoggingModule:ILoggingModule
   {        
      public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding)
      {

         logBinding
            .To<NLogLogger>()
            .InScope(GetTypeOfTarget)
            .WithConstructorArgument("receivingType", (context, target) => GetTypeOfTarget(context));

         eventPublisherBinding
            .To<LogBasedEventPublisher>()
            .InSingletonScope();
      }


      private static Type GetTypeOfTarget(IContext context)
      {
         var type = context.Request.Target?.Member.DeclaringType;
         return type ?? typeof(SampleLoggingModule);
      }
   }
}