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
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public sealed class ConsulServiceListMonitor : IServiceListMonitor
    {
        private const string ConsulServiceList = "ConsulServiceList";

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

        public ConsulServiceListMonitor(ILog log, ConsulClient consulClient, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig, IHealthMonitor healthMonitor)
        {
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;            
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();

            _serviceListHealthMonitor = healthMonitor.SetHealthFunction(ConsulServiceList, () => _healthStatus);
            LoopingTask = GetAllLoop();
        }


        public string DataCenter { get; }

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

        public int Version { get; private set; }

        private async Task GetAllLoop()
        {
            _initTask = GetAll(0);
            var modifyIndex = await _initTask.ConfigureAwait(false);
            while (!ShutdownToken.IsCancellationRequested)
            {
                modifyIndex = await GetAll(modifyIndex).ConfigureAwait(false);
            }
        }

        public Task Init() => _initTask;

        private async Task<ulong> GetAll(ulong modifyIndex)
        {
            await WaitIfErrorOccuredOnPreviousCall().ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand =
                $"v1/kv/service?dc={DataCenter}&keys&index={modifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var consulResult = await ConsulClient.Call<string[]>(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (consulResult.IsSuccessful && consulResult.Response!=null)
            {
                var allKeys = consulResult.Response;
                var allServiceNames = allKeys.Select(s => s.Substring("service/".Length));
                _allServices = new HashSet<string>(allServiceNames)
                    .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

                if (allKeys.Length == _allServices.Count)
                    _healthStatus = HealthCheckResult.Healthy(string.Join("\r\n", allKeys));
                else
                    _healthStatus = HealthCheckResult.Unhealthy("Service list contains duplicate services: " + string.Join(", ", GetDuplicateServiceNames(allKeys)));

                Version++;

                return consulResult.ModifyIndex ?? 0;
            }
            else
            {
                SetErrorResult(consulResult, "Error calling Consul all-keys");
                _healthStatus = HealthCheckResult.Unhealthy("Error calling Consul: " + consulResult.Error.Message);
                return 0;
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


        private async Task WaitIfErrorOccuredOnPreviousCall()
        {
            if (Error != null)
            {
                var config = GetConfig();
                var now = DateTime.UtcNow;
                var timeElapsed = ErrorTime - now;
                if (timeElapsed < config.ErrorRetryInterval)
                    await DateTime.Delay(config.ErrorRetryInterval - timeElapsed, ShutdownToken.Token).ConfigureAwait(false);
            }
        }

        private void SetErrorResult<T>(ConsulResult<T> result, string errorMessage)
        {
            var error = result.Error ?? new EnvironmentException(errorMessage);

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


        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            ShutdownToken?.Cancel();
            LoopingTask.GetAwaiter().GetResult();
            ShutdownToken?.Dispose();
            _serviceListHealthMonitor.Dispose();
        }
        
    }

}
