using System;
using Metrics.Json;
using Metrics.RemoteMetrics;

namespace Metrics
{
    public static class RemoteMetricsExtensions
    {
        private static Func<string, JsonMetricsContext> jsonDeserializer;


        public static MetricsConfig WithJsonDeserialzier(this MetricsConfig config, Func<string, JsonMetricsContext> jsonDeserializer)
        {
            RemoteMetricsExtensions.jsonDeserializer = jsonDeserializer;
            return config;
        }

        public static MetricsConfig RegisterRemote(this MetricsConfig config, string name, Uri remoteUri, TimeSpan updateInterval)
        {
            if (jsonDeserializer == null)
            {
                throw new InvalidOperationException("You must set a JSON Deserializer by setting Metrics.Config.WithJsonDeserialzier()");
            }

            config.WithConfigExtension((ctx, hs) => ctx.Advanced.AttachContext(name, new RemoteMetricsContext(remoteUri, updateInterval, jsonDeserializer)), () => false);
            return config;
        }
    }
}
