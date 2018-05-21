using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.SharedLogic.Rewrite;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors Consul using KeyValue api, to get a list of all available <see cref="Services"/>.
    /// Creates instances of ConsulNodeSourceFactory using the list of known services.
    /// </summary>
    internal sealed class ConsulNodeSourceFactory : INodeSourceFactory, IDisposable
    {
        private Func<DeploymentIdentifier, ConsulNodeSource> CreateConsulNodeSource { get; }
        private CancellationTokenSource ShutdownToken { get; }

        private int _disposed;
        private readonly object  _initLock = new object();        
        private Task<ulong> _initTask;

        ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private HealthCheckResult _healthStatus = HealthCheckResult.Healthy();
        private readonly ComponentHealthMonitor _serviceListHealthMonitor;
        private Task LoopingTask { get; set; }

        /// <inheritdoc />
        public ConsulNodeSourceFactory(ILog log, ConsulClient consulClient, Func<DeploymentIdentifier, ConsulNodeSource> createConsulNodeSource, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig, IHealthMonitor healthMonitor)
        {
            Log = log;
            ConsulClient = consulClient;
            CreateConsulNodeSource = createConsulNodeSource;
            DateTime = dateTime;
            GetConfig = getConfig;            
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();

            _serviceListHealthMonitor = healthMonitor.SetHealthFunction("ConsulServiceList", () => _healthStatus);
        }

        public string Type => "Consul";

        public async Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier deploymentIdentifier)
        {
            await Init().ConfigureAwait(false);

            if (ServiceExists(deploymentIdentifier))
            {
                var consulNodeSource = CreateConsulNodeSource(deploymentIdentifier);
                await consulNodeSource.Init().ConfigureAwait(false);
                return consulNodeSource;
            }
            return null;
        }

        private string DataCenter { get; }

        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }


        private bool ServiceExists(DeploymentIdentifier deploymentIdentifier)
        {
            if (Services.Count == 0 && Error != null)
                throw Error;

            return Services.Contains(deploymentIdentifier.ToString());
        }

        HashSet<string> Services = new HashSet<string>();


        private async Task GetAllLoop()
        {
            try
            {
                var modifyIndex = await _initTask.ConfigureAwait(false);
                while (!ShutdownToken.IsCancellationRequested)
                {
                    // If we got an error, we don't want to spam Consul so we wait a bit
                    if (Error != null)
                        await DateTime.DelayUntil(ErrorTime + GetConfig().ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);
                    modifyIndex = await GetAllServices(modifyIndex).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }

        /// <inheritdoc />
        private Task Init()
        {
            if (_initTask == null)
                lock (_initLock)
                {
                    if (_initTask == null)
                    {
                        _initTask = GetAllServices(0);
                        LoopingTask = Task.Run(GetAllLoop);
                    }
                }
  
            return _initTask;            
        }


        private async Task<ulong> GetAllServices(ulong modifyIndex)
        {
            var consulResult = await ConsulClient.GetAllServices(modifyIndex, ShutdownToken.Token).ConfigureAwait(false);

            if (consulResult.Error != null)
            {
                SetErrorResult(consulResult);
                _healthStatus = HealthCheckResult.Unhealthy($"Error calling Consul: {consulResult.Error.Message}");
                return 0;
            }
            else
            {
                var servicesResult = consulResult.Result;

                Services = new HashSet<string>(servicesResult);
                var caseInsensitiveServices = new HashSet<string>(servicesResult, StringComparer.InvariantCultureIgnoreCase);

                if (Services.Count == caseInsensitiveServices.Count)
                    _healthStatus = HealthCheckResult.Healthy(string.Join("\r\n", Services));
                else
                    _healthStatus = HealthCheckResult.Unhealthy("Service list contains duplicate services: " + string.Join(", ", GetDuplicateServiceNames(servicesResult)));

                Error = null;
                return consulResult.ModifyIndex ?? 0;
            }
        }

        private string[] GetDuplicateServiceNames(IEnumerable<string> allServices)
        {
            var list = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var duplicateList = new HashSet<string>();
            foreach (var service in allServices)
            {
                if (list.Contains(service))
                {
                    var existingService = list.First(x => x.Equals(service, StringComparison.CurrentCultureIgnoreCase));
                    duplicateList.Add(existingService);
                    duplicateList.Add(service);
                }
                list.Add(service);
            }
            return duplicateList.ToArray();
        }

        
        private void SetErrorResult<T>(ConsulResponse<T> response)
        {
            var error = response.Error;

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error("Error calling Consul to get all services list", exception: response.Error, unencryptedTags: new
                {
                    consulAddress = response.ConsulAddress,
                    commandPath = response.CommandPath,
                    responseCode = response.StatusCode,
                    content = response.ResponseContent
                });
            }

            Error = error;
            ErrorTime = DateTime.UtcNow;            
        }


        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            ShutdownToken.Cancel();
            try
            {
                LoopingTask?.Wait(TimeSpan.FromSeconds(3));
            }
            catch (TaskCanceledException) {}
            ShutdownToken.Dispose();
            _serviceListHealthMonitor.Dispose();
        }
        
    }

}
