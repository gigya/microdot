
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Metrics.Logging;
namespace Metrics
{
    public class MetricsErrorHandler
    {
        private static readonly ILog log = LogProvider.GetCurrentClassLogger();
        private static readonly Meter errorMeter = Metric.Internal.Meter("Metrics Errors", Unit.Errors);

        private readonly ConcurrentBag<Action<Exception, string>> handlers = new ConcurrentBag<Action<Exception, string>>();

        private static readonly bool isMono = Type.GetType("Mono.Runtime") != null;

        static MetricsErrorHandler() { }

        private MetricsErrorHandler()
        {
            this.AddHandler((x, msg) => log.ErrorException("Metrics: Unhandled exception in Metrics.NET Library {0} {1}", x, msg, x.Message));
            this.AddHandler((x, msg) => Trace.TraceError("Metrics: Unhandled exception in Metrics.NET Library " + x.ToString()));

            if (Environment.UserInteractive || isMono)
            {
                this.AddHandler((x, msg) => Console.WriteLine("Metrics: Unhandled exception in Metrics.NET Library {0} {1}", msg, x.ToString()));
            }
        }

        internal static MetricsErrorHandler Handler { get; } = new MetricsErrorHandler();

        internal void AddHandler(Action<Exception> handler)
        {
            AddHandler((x, msg) => handler(x));
        }

        internal void AddHandler(Action<Exception, string> handler)
        {
            this.handlers.Add(handler);
        }

        internal void ClearHandlers()
        {
            while (!this.handlers.IsEmpty)
            {
                Action<Exception, string> item;
                this.handlers.TryTake(out item);
            }
        }

        private void InternalHandle(Exception exception, string message)
        {
            errorMeter.Mark();

            foreach (var handler in this.handlers)
            {
                try
                {
                    handler(exception, message);
                }
                catch
                {
                    // error handler throw-ed on us, hope you have a debugger attached.
                }
            }
        }

        public static void Handle(Exception exception)
        {
            Handle(exception, string.Empty);
        }

        public static void Handle(Exception exception, string message)
        {
            Handler.InternalHandle(exception, message);
        }
    }
}
