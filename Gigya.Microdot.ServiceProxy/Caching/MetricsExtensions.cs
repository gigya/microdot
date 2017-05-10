using System.Linq;

using Metrics;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    internal static class MetricsExtensions
    {
        public static void Mark(this MetricsContext context, string[] keys)
        {
            context.Meter("All", Unit.Calls).Mark();

            for (int i = 0; i < keys.Length; i++)
            {
                var aggregateKey = string.Join(".", keys.Take(i + 1));
                context.Meter(aggregateKey, Unit.Calls).Mark();
            }

        }

        public static void Increment(this MetricsContext context, string[] keys)
        {
            context.Counter("All", Unit.Calls).Increment();

            for (int i = 0; i < keys.Length; i++)
            {
                var aggregateKey = string.Join(".", keys.Take(i + 1));
                context.Counter(aggregateKey, Unit.Calls).Increment();
            }

        }

        public static void Decrement(this MetricsContext context, string[] keys)
        {
            context.Counter("All", Unit.Calls).Decrement();

            for (int i = 0; i < keys.Length; i++)
            {
                var aggregateKey = string.Join(".", keys.Take(i + 1));
                context.Counter(aggregateKey, Unit.Calls).Decrement();
            }
        }
    }
}