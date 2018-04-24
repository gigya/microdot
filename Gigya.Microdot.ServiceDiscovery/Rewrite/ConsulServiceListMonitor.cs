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

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public sealed class ConsulServiceListMonitor : IServiceListMonitor
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

        public ConsulServiceListMonitor(ILog log, ConsulClient consulClient, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();

            GetAllLoop();
        }

        public string DataCenter { get; }

        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

        public ImmutableHashSet<string> Services
        {
            get
            {
                if (_allServices.Count == 0 && Error != null)
                    throw Error;
                return _allServices;
            }
        }

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

            if (consulResult.Success && consulResult.Response!=null)
            {
                var allKeys = consulResult.Response;
                var allServiceNames = allKeys.Select(s => s.Substring("service/".Length));
                _allServices = new HashSet<string>(allServiceNames)
                    .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

                return consulResult.ModifyIndex ?? 0;
            }
            else
            {
                SetErrorResult(consulResult, "Error calling Consul all-keys");
                return 0;
            }
        }


        private async Task WaitIfErrorOccuredOnPreviousCall()
        {
            if (Error != null)
            {
                var config = GetConfig();
                var now = DateTime.UtcNow;
                var timeElapsed = ErrorTime - now;
                if (timeElapsed < config.ErrorRetryInterval)
                    await DateTime.Delay(config.ErrorRetryInterval - timeElapsed).ConfigureAwait(false);
            }
        }

        private void SetErrorResult<T>(ConsulResult<T> result, string errorMessage)
        {
            var error = result.Error ?? new EnvironmentException(errorMessage);

            if (!(error is TaskCanceledException))
                Log.Error("Error calling Consul to get all services list", exception: result.Error, unencryptedTags: new
                {
                    ConsulAddress = ConsulClient.ConsulAddress.ToString(),
                    consulQuery = result.RequestLog,
                    ResponseCode = result.StatusCode,
                    Content = result.ResponseContent
                });

            Error = error;
            ErrorTime = DateTime.UtcNow;            
        }


        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }
        
    }

}
