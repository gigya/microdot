using System;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject.Interfaces;
using Ninject.Activation;
using Ninject.Modules;
using Ninject.Syntax;

namespace Gigya.Microdot.Logging.NLog
{
    /// <summary>
    /// Configures the logger to be <see cref="NLogLogger"/> (instance per type) and the event publisher to be
    /// <see cref="NullEventPublisher"/>.
    /// </summary>
    public class NLogModule : NinjectModule, ILoggingModule
    {
        /// <summary>
        /// Used by clients to initialize logging and tracing.
        /// </summary>
        public override void Load()
        {
            Bind(Bind<ILog>(), Bind<IEventPublisher>());
        }

        /// <summary>
        /// Used by Microdot hosts to initialize logging and tracing.
        /// </summary>
        /// <param name="logBinding"></param>
        /// <param name="eventPublisherBinding"></param>
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

        /// <summary>
        /// Returns the type that requested the log, or the type <see cref="NLogModule"/> if the requester can't be determined.
        /// </summary>
        /// <param name="context">The Ninject context of the request.</param>
        /// <returns></returns>
        private static Type GetTypeOfTarget(IContext context)
        {
            var type = context.Request.Target?.Member.DeclaringType;
            return type ?? typeof(NLogModule);
        }
    }
}