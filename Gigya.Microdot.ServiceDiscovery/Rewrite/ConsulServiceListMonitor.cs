using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Monitors Consul using KeyValue api, to get a list of all available services
    /// </summary>
    public sealed class ConsulServiceListMonitor: IConsulServiceListMonitor
    {
        private CancellationTokenSource ShutdownToken { get; }

        /// <summary>
        /// Result of all keys on Consul
        /// </summary>
        private ImmutableHashSet<string> _allServices = new HashSet<string>().ToImmutableHashSet();

        private int _disposed;
        private Task<ulong> _initTask;

        ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private HealthCheckResult _healthStatus = HealthCheckResult.Healthy();
        private readonly ComponentHealthMonitor _serviceListHealthMonitor;
        private Task LoopingTask { get; set; }

        /// <inheritdoc />
        public ConsulServiceListMonitor(ILog log, ConsulClient consulClient, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig, IHealthMonitor healthMonitor)
        {
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;            
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();

            _serviceListHealthMonitor = healthMonitor.SetHealthFunction("ConsulServiceList", () => _healthStatus);
            LoopingTask = GetAllLoop();
        }


        private string DataCenter { get; }

        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

        /// <inheritdoc />
        public ImmutableHashSet<string> Services
        {
            get
            {
                if (_allServices.Count == 0 && Error != null)
                    throw Error;
                return _allServices;
            }
        }

        /// <inheritdoc />
        public int Version { get; private set; }

        private async Task GetAllLoop()
        {
            try
            {
                _initTask = GetAll(0);
                var modifyIndex = await _initTask.ConfigureAwait(false);
                while (!ShutdownToken.IsCancellationRequested)
                {
                    modifyIndex = await GetAll(modifyIndex).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }

        /// <inheritdoc />
        public Task Init() => _initTask;

        private async Task<ulong> GetAll(ulong modifyIndex)
        {
            ConsulConfig config = GetConfig();

            if (Error != null)
                await DateTime.DelayUntil(ErrorTime + config.ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);

            double maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            string urlCommand =
                $"v1/kv/service?dc={DataCenter}&keys&index={modifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var consulResult = await ConsulClient.Call<string[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (consulResult.Error != null)
            {
                SetErrorResult(consulResult);
                _healthStatus = HealthCheckResult.Unhealthy($"Error calling Consul: {consulResult.Error.Message}");
            }
            else if (consulResult.Response != null)
            {
                var allKeys = consulResult.Response;
                var allServiceNames = allKeys.Select(s => s.Substring("service/".Length));
                _allServices = new HashSet<string>(allServiceNames)
                    .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

                Version++;

                if (allKeys.Length == _allServices.Count)
                    _healthStatus = HealthCheckResult.Healthy(string.Join("\r\n", allKeys));
                else
                    _healthStatus = HealthCheckResult.Unhealthy("Service list contains duplicate services: " + string.Join(", ", GetDuplicateServiceNames(allKeys)));

                Error = null;
                return consulResult.ModifyIndex ?? 0;
            }
            return 0;
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

        
        private void SetErrorResult<T>(ConsulResult<T> result)
        {
            var error = result.Error;

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error("Error calling Consul to get all services list", exception: result.Error, unencryptedTags: new
                {
                    consulAddress = result.ConsulAddress,
                    commandPath = result.CommandPath,
                    responseCode = result.StatusCode,
                    content = result.ResponseContent
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

            ShutdownToken?.Cancel();
            try
            {
                LoopingTask.GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) {}
            ShutdownToken?.Dispose();
            _serviceListHealthMonitor.Dispose();
        }
        
    }

}
