using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public sealed class ConsulClient: ConsulClientBase, IConsulClient
    {
        private CancellationTokenSource ShutdownToken { get; }

        /// <summary>
        /// Current ModifyIndex of get-all-keys api on Consul (/v1/kv/service?keys)
        /// </summary>
        private ulong _allKeysModifyIndex = 0;

        /// <summary>
        /// Result of all keys on Consul
        /// </summary>
        private string[] _allKeys;

        private Task _getAllKeys;
        private int _disposed;

        public ConsulClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig):
            base(log,environmentVariableProvider,dateTime,getConfig)
        {
            ShutdownToken = new CancellationTokenSource();
            _getAllKeys = GetAllKeys();
        }

        private async Task GetAllKeys()
        {
            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand =
                $"v1/kv/service?dc={DataCenter}&keys&index={_allKeysModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, ShutdownToken.Token).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                _allKeysModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                _allKeys = TryDeserialize<string[]>(response.ResponseContent);
            }
            else
            {
                Log.Warn("Error calling Consul all-keys", unencryptedTags: new
                {
                    requestLog = urlCommand,
                    consulResponse = response.ResponseContent,
                    ConsulAddress
                });
                await DateTime.Delay(config.ErrorRetryInterval).ConfigureAwait(false);
            }

            if (!ShutdownToken.IsCancellationRequested)
                _getAllKeys = GetAllKeys();
        }

        public async Task LoadNodes(ConsulServiceState serviceState)
        {
            InitGetAllKeys();
            await WaitIfErrorOccuredOnPreviousCall(serviceState).ConfigureAwait(false);

            if (!serviceState.IsDeployed)
                return;

            if (serviceState.ActiveVersion == null)
                await LoadServiceVersion(serviceState).ConfigureAwait(false);

            if (serviceState.NodesLoading == null || serviceState.NodesLoading.IsCompleted)
                serviceState.NodesLoading = LoadNodesByHealth(serviceState);
            if (serviceState.VersionLoading == null || serviceState.VersionLoading.IsCompleted)
                serviceState.VersionLoading = LoadServiceVersion(serviceState);

            await Task.WhenAny(serviceState.NodesLoading, serviceState.VersionLoading);
        }

        private async Task LoadServiceVersion(ConsulServiceState serviceState)
        {
            InitGetAllKeys();
            await WaitIfErrorOccuredOnPreviousCall(serviceState).ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/kv/service/{serviceState.ServiceName}?dc={DataCenter}&index={serviceState.VersionModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, serviceState.ShutdownToken).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                serviceState.VersionModifyIndex = response.ModifyIndex.Value;

            if (response.IsDeployed == false)
            {
                var serviceExists = await SearchServiceInAllKeys(serviceState).ConfigureAwait(false);
                if (serviceExists)
                {
                    await ReloadServiceVersion(serviceState).ConfigureAwait(false);
                    return;
                }
                else
                {
                    SetServiceMissingResult(serviceState, response);
                    return;
                }
            }
            else if (response.Success)
            {
                var keyValue = TryDeserialize<KeyValueResponse[]>(response.ResponseContent);
                var version = keyValue?.SingleOrDefault()?.TryDecodeValue()?.Version;

                if (version != null)
                {
                    lock (serviceState)
                    {
                        serviceState.ActiveVersion = version;
                        serviceState.IsDeployed = true;
                    }
                    return;
                }
            }
            SetErrorResult(serviceState, response, "Cannot extract service's active version from Consul response");
        }

        private void InitGetAllKeys()
        {
            if (_getAllKeys == null)
                _getAllKeys = GetAllKeys();
        }

        private Task ReloadServiceVersion(ConsulServiceState serviceState)
        {
            serviceState.VersionModifyIndex = 0;
            return LoadServiceVersion(serviceState);
        }

        private async Task<bool> SearchServiceInAllKeys(ConsulServiceState serviceState)
        {
            if (_allKeys == null)
            {
                InitGetAllKeys();
                await _getAllKeys.ConfigureAwait(false);
            }

            var serviceNameMatchByCase = _allKeys?.FirstOrDefault(s =>
                    s.Equals($"service/{serviceState.ServiceName}", StringComparison.InvariantCultureIgnoreCase))
                    ?.Substring("service/".Length);

            var serviceExists = serviceNameMatchByCase != null;

            if (!serviceExists)
                return false;

            if (serviceState.ServiceName != serviceNameMatchByCase)
            {
                Log.Warn("Requested service found on Consul with different case", unencryptedTags: new
                {
                    requestedService = serviceState.ServiceNameOrigin,
                    serviceOnConsul = serviceNameMatchByCase
                });
                serviceState.ServiceName = serviceNameMatchByCase;
            }

            return true;
        }

        private async Task LoadNodesByHealth(ConsulServiceState serviceState)
        {
            if (!serviceState.IsDeployed)
                return;

            await WaitIfErrorOccuredOnPreviousCall(serviceState).ConfigureAwait(false);

            var config = GetConfig();
            var maxSecondsToWaitForResponse = Math.Max(0, config.HttpTimeout.TotalSeconds - 2);
            var urlCommand = $"v1/health/service/{serviceState.ServiceName}?dc={DataCenter}&passing&index={serviceState.HealthModifyIndex}&wait={maxSecondsToWaitForResponse}s";
            var response = await CallConsul(urlCommand, serviceState.ShutdownToken).ConfigureAwait(false);

            if (response.ModifyIndex.HasValue)
                serviceState.HealthModifyIndex = response.ModifyIndex.Value;

            if (response.Success)
            {
                var nodes = TryDeserialize<ServiceEntry[]>(response.ResponseContent);
                if (nodes != null)
                {
                    if (
                        // Service has no nodes, but it did did have nodes before, and it is deployed
                        (nodes.Length == 0 && serviceState.Nodes.Length != 0 && serviceState.IsDeployed)
                        // Service has nodes, but it is not deployed
                        || (nodes.Length > 0 && !serviceState.IsDeployed))
                    {
                        // Try to reload version, to check if service deployment has changed
                        await ReloadServiceVersion(serviceState).ConfigureAwait(false);
                        if (serviceState.IsDeployed)
                        {
                            await LoadNodesByHealth(serviceState).ConfigureAwait(false);
                            return;
                        }
                    }
                    SetConsulNodes(nodes, serviceState, response, filterByVersion: true);
                    return;
                }
            }            
            SetErrorResult(serviceState, response, "Cannot extract service's nodes from Consul response");            
        }

        private async Task WaitIfErrorOccuredOnPreviousCall(ConsulServiceState serviceState)
        {
            if (serviceState.LastResult?.Error != null)
            {
                var config = GetConfig();
                var now = DateTime.UtcNow;
                var timeElapsed = serviceState.LastResult.ResponseDateTime - now;
                if (timeElapsed < config.ErrorRetryInterval)
                    await DateTime.Delay(config.ErrorRetryInterval - timeElapsed).ConfigureAwait(false);
            }            
        }

        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            base.Dispose();
            HttpClient?.Dispose();
            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }
    }
}

