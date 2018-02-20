using System;
using System.Net;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Metrics.Logging;
using ILog = Gigya.Microdot.Interfaces.Logging.ILog;

namespace Gigya.Microdot.Hosting.Metrics
{
    public sealed class MetricsInitializer : IMetricsInitializer
    {
        private bool _disposed;
        public MetricsConfig MetricsConfig { get; private set; }
        private IMetricsSettings MetricsSettings { get; }
        public HealthMonitor HealthMonitor { get; set; }
        private ILog Log { get; }
        private IEnvironmentVariableProvider EnvProvider { get; }


        public MetricsInitializer(ILog log, IMetricsSettings metricsSettings, HealthMonitor healthMonitor, IEnvironmentVariableProvider envProvider)
        {
            MetricsSettings = metricsSettings;
            HealthMonitor = healthMonitor;
            Log = log;
            EnvProvider = envProvider;
        }

        public void Init()
        {
            var metricsPort = MetricsSettings.MetricsPort;
            Log.Info(_ => _("Initializing Metrics", unencryptedTags: new { port = metricsPort }));

            LogProvider.SetCurrentLogProvider(null);
            Exception metricsException = null;

            Metric.Config.WithErrorHandler(ex =>
            {
                if (metricsException != null)
                    metricsException = ex;
            }, true);

            MetricsConfig = Metric.Config.WithHttpEndpoint($"http://+:{metricsPort}/");
            var initTask = MetricsConfig.WhenEndpointInitialized();
            initTask.Wait(TimeSpan.FromSeconds(30));
            if (!initTask.IsCompleted)
            {
                throw new EnvironmentException("Metrics.NET could not be initialized. Timeout after 30 seconds.");
            }

            if (metricsException != null)
            {
                var exception = metricsException as HttpListenerException;
                if (exception != null && exception.ErrorCode == 5)
                {
                    throw new EnvironmentException(
                        $"Port {metricsPort} defined for Metrics.NET wasn't configured to run without administrative premissions.\nRun:\n" +
                        $"netsh http add urlacl url=http://+:{metricsPort}/ user={CurrentApplicationInfo.OsUser}", metricsException);
                }

                throw new EnvironmentException("Problem loading metrics.net", metricsException);
            }

            Metric.Config.WithErrorHandler(ex =>
            {
                Log.Error(_ => _("Metrics Error", exception: ex));
            }, true);
        }


        public void Dispose()
        {
            if (_disposed)
                return;
            try
            {
                _disposed = true;
                MetricsConfig.Dispose();
                HealthMonitor.Dispose();                
            }
            catch (AggregateException ae)
            {
                // Ignore all TaskCanceledExceptions (unhandled by Metrics.NET for unknown reasons)
                ae.Handle(ex => ex is TaskCanceledException);
            }
        }
    }
}
