using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Logging;
using Metrics;
using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.Fakes
{
    public sealed class MetricsInitializerFake : IMetricsInitializer
    {

        public void Init()
        {
            Metrics.Logging.LogProvider.SetCurrentLogProvider(null);

            MetricsConfig = Metric.Config.WithErrorHandler(ex => {
                Log.Error(_ => _("", ex));
            }, true);
        }

        public void Dispose()
        {
            try
            {
                MetricsConfig.Dispose();
            }
            catch (AggregateException ae)
            {
                // Ignore all TaskCanceledExceptions (unhandled by Metrics.NET for unknown reasons)
                ae.Handle(ex => ex is TaskCanceledException);
            }
        }

        public MetricsConfig MetricsConfig { get; private set; }

        private IMetricsSettings MetricsSettings { get; set; }

        private ILog Log { get; }

        public MetricsInitializerFake(ILog log, IMetricsSettings metricsSettings)
        {
            MetricsSettings = metricsSettings;
            Log = log;
        }

    }
}
