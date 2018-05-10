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
    /// Monitors Consul using KeyValue api, to get a list of all available <see cref="Services"/>.
    /// Increments <see cref="Version"/> whenever the list is modified.
    /// </summary>
    public sealed class ConsulServiceListMonitor: IConsulServiceListMonitor
    {
        private CancellationTokenSource ShutdownToken { get; }

        private int _disposed;
        private int _initiated;
        private readonly TaskCompletionSource<bool> _waitForInitiation = new TaskCompletionSource<bool>();
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
        }


        private string DataCenter { get; }

        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

        // TODO: clients cannot obtain Services and the corresponding Version atomically. The order of checking these properties is important.
        // expose ServicesListAndVersion instead
        /// <inheritdoc />
        public ImmutableHashSet<string> Services
        {
            get
            {
                if (_servicesAndVersion.Services.Count == 0 && Error != null)
                    throw Error;
                return _servicesAndVersion.Services;
            }
        }

        /// <inheritdoc />
        public int Version => _servicesAndVersion.Version;

        class ServicesListAndVersion
        {
            /// <summary>
            /// Result of all keys on Consul
            /// </summary>
            public ImmutableHashSet<string> Services = new HashSet<string>().ToImmutableHashSet();
            public int Version;
        }

        ServicesListAndVersion _servicesAndVersion;

        private async Task GetAllLoop()
        {
            try
            {
                _initTask = GetAll(0);
                _waitForInitiation.TrySetResult(true);

                var modifyIndex = await _initTask.ConfigureAwait(false);
                while (!ShutdownToken.IsCancellationRequested)
                {
                    // If we got an error, we don't want to spam Consul so we wait a bit
                    if (Error != null)
                        await DateTime.DelayUntil(ErrorTime + GetConfig().ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);
                    modifyIndex = await GetAll(modifyIndex).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }

        /// <inheritdoc />
        public async Task Init()
        {
            if (Interlocked.Increment(ref _initiated) == 1)
            {
                LoopingTask = GetAllLoop();
            }

            await _waitForInitiation.Task.ConfigureAwait(false);
            await _initTask.ConfigureAwait(false);

            // If we leave _initiated without change, it might get to int.Max and then Interlocked.Increment may put it back to int.Min.
            // At some point, it might get back to zero. To prevent it, we set it back to a lower value.
            _initiated = 2;
        }
        

        private async Task<ulong> GetAll(ulong modifyIndex)
        {
            string urlCommand =
                $"v1/kv/service?dc={DataCenter}&keys&index={modifyIndex}&wait={GetConfig().HttpTimeout.TotalSeconds}s";
            var consulResult = await ConsulClient.Call<string[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (consulResult.Error != null)
            {
                SetErrorResult(consulResult);
                _healthStatus = HealthCheckResult.Unhealthy($"Error calling Consul: {consulResult.Error.Message}");
                return 0;
            }
            else
            {
                var allKeys = consulResult.Response;
                var allServiceNames = allKeys.Select(s => s.Substring("service/".Length));
                var newServices = new HashSet<string>(allServiceNames)
                    .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

                if (!newServices.SequenceEqual(_servicesAndVersion.Services))
                    _servicesAndVersion = new ServicesListAndVersion { // update atomically
                        Services = newServices,
                        Version = _servicesAndVersion.Version + 1
                    };

                if (allKeys.Length == _servicesAndVersion.Services.Count)
                    _healthStatus = HealthCheckResult.Healthy(string.Join("\r\n", _servicesAndVersion.Services));
                else
                    _healthStatus = HealthCheckResult.Unhealthy("Service list contains duplicate services: " + string.Join(", ", GetDuplicateServiceNames(allKeys)));

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
