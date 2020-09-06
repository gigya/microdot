using Gigya.Common.Application;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public class AvailabilityZoneServiceDiscovery
    {
        public AvailabilityZoneInfo Info { get; } = new AvailabilityZoneInfo();
        public TimeSpan DiscoveryGetNodeTimeoutInMs { get; set; } = TimeSpan.FromMilliseconds(1000);

        private readonly string _serviceName;
        private readonly CancellationToken _disposeCancellationToken;
        private readonly Rewrite.IDiscovery _discovery;
        private readonly Rewrite.IConsulClient _consulClient;
        private readonly ILog _log;
        private readonly ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();

        private readonly TaskCompletionSource<bool> _initialReadZonesTask = new TaskCompletionSource<bool>();

        public AvailabilityZoneServiceDiscovery(
            string serviceName,
            CancellationToken ctk, IDiscovery discovery,
            Rewrite.IConsulClient consulClient,
            ILog log)
        {
            _serviceName = serviceName;
            _disposeCancellationToken = ctk;
            _discovery = discovery;
            _consulClient = consulClient;
            _log = log;

            Task.Run(UpdateAndMonitorAvailabilityZoneAsync, _disposeCancellationToken);
        }

        public async Task<bool> HandleEnvironmentChangesAsync()
        {
            await _initialReadZonesTask.Task;

            bool changeOccured = false;
            if (Info.DeploymentIdentifier == null)
                throw new EnvironmentException("Failed to define deployment identifier. Missing or invalid zone information.");

            var nodes = await _discovery.GetNodes(Info.DeploymentIdentifier).WithTimeout(DiscoveryGetNodeTimeoutInMs);
            if (nodes == null)
            {
                using (new ReaderWriterLocker(_readerWriterLock, ReaderWriterLocker.LockType.WriteLock))
                {
                    Info.Nodes = nodes;
                }

                throw new EnvironmentException(
                        $"Failed to GetNodes via IDiscovery for service {Info.DeploymentIdentifier.ServiceName}");
            }

            var nodesNames = nodes.Select(x => x.ToString());

            using (new ReaderWriterLocker(_readerWriterLock, ReaderWriterLocker.LockType.ReadLock))
            {
                if (Info.Nodes == null || Info.Nodes.Select(x => x.ToString()).SequenceEqual(nodesNames) == false)
                {
                    changeOccured = true;
                    _log.Info(x => x($"{_serviceName} nodes change applied according to Zone: {Info.ServiceZone}"));
                }
            }

            if (changeOccured)
            {
                using (new ReaderWriterLocker(_readerWriterLock, ReaderWriterLocker.LockType.WriteLock))
                {
                    Info.Nodes = nodes;

                    if (Info.Nodes.Length < 1)
                        throw new EnvironmentException($"Failed to CreateConnection. No nodes discovered for Service: {Info.DeploymentIdentifier?.ServiceName} Zone: {Info.DeploymentIdentifier?.Zone} (Service Zone: {Info.ServiceZone})");
                }
            }

            return changeOccured;
        }

        private async Task UpdateAndMonitorAvailabilityZoneAsync()
        {
            const string folder = "ZoneSettings";
            ulong modifyIndex = 0;
            while (_disposeCancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    var response = await _consulClient.GetKey<DbKeyValue>(modifyIndex, folder, _serviceName, _disposeCancellationToken);
                    if (response == null)
                    {
                        Info.StatusCode = AvailabilityZoneInfo.StatusCodes.FailedConnectToConsul;
                        Info.Exception = new EnvironmentException($"Failed to connect to consul with {folder}/{_serviceName} key. Service Unavailable");
                        _log.Warn(Info.Exception.Message, Info.Exception);
                    }
                    else if (response.StatusCode == null ||
                             response.StatusCode >= HttpStatusCode.InternalServerError ||
                             response.StatusCode < HttpStatusCode.OK)
                    {
                        Info.StatusCode = AvailabilityZoneInfo.StatusCodes.FailedConnectToConsul;
                        Info.Exception = new EnvironmentException($"Failed to connect to consul with {folder}/{_serviceName} key. Service returned error status: '{(response.StatusCode?.ToString() ?? "NULL")}'");
                        _log.Warn(Info.Exception.Message, Info.Exception);
                    }
                    else if (response.Error != null)
                    {
                        Info.StatusCode = AvailabilityZoneInfo.StatusCodes.MissingOrInvalidKeyValue;
                        Info.Exception = response.Error;
                        _log.Warn(Info.Exception.Message, Info.Exception);
                    }
                    else
                    {
                        modifyIndex = response.ModifyIndex ?? 0;
                        await SetDeploymentIdentifierAsync(response.ResponseObject, _disposeCancellationToken);
                        _initialReadZonesTask.TrySetResult(default);
                    }

                    _initialReadZonesTask.TrySetResult(default);
                }
                catch (Exception e)
                {
                    _log.Critical("", exception: e);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), _disposeCancellationToken);
            }
        }

        private async Task SetDeploymentIdentifierAsync(DbKeyValue keyValueFromConsul, CancellationToken ctk)
        {
            var activeZone = keyValueFromConsul.ServiceZone.ToLower();
            var activeConsulZone = keyValueFromConsul.ConsulZone.ToLower();

            Info.ServiceZone = activeZone;
            var componentNameCandidate = $"{_serviceName}_{activeZone}";

            if (Info.DeploymentIdentifier?.ServiceName != componentNameCandidate ||
                Info.DeploymentIdentifier?.Zone != activeConsulZone ||
                Info.StatusCode == AvailabilityZoneInfo.StatusCodes.Ok)
            {
                var tempDeploymentIdentifier = new DeploymentIdentifier(componentNameCandidate,
                    deploymentEnvironment: null, activeConsulZone); // null because of microdot discovery bug
                var respNodes = await _consulClient.GetHealthyNodes(tempDeploymentIdentifier, modifyIndex: 0, ctk);

                if (respNodes.StatusCode != HttpStatusCode.OK || respNodes.ResponseObject.Length <= 0)
                {
                    if (Info.DeploymentIdentifier != null)
                    {
                        Info.Exception = new EnvironmentException($"Cannot get healthy nodes from {componentNameCandidate}\nKeeping activity on cluster {Info.DeploymentIdentifier?.ServiceName} Zone:{Info.DeploymentIdentifier?.Zone}");
                        _log.Error(Info.Exception.Message);
                        Info.StatusCode = AvailabilityZoneInfo.StatusCodes.FailedGetHealthyNodes;
                        return;
                    }

                    // Can't init DeploymentIdentifier on startup.. throw
                    Info.Exception = new EnvironmentException(
                        $"Cannot get healthy nodes from {componentNameCandidate}. Probably bad consul configuration. Check consul->KeyValue->ZoneSettings->{_serviceName} for correct settings");
                    _log.Critical(Info.Exception.Message);
                    throw Info.Exception;
                }

                Info.DeploymentIdentifier = tempDeploymentIdentifier;
                Info.StatusCode = AvailabilityZoneInfo.StatusCodes.Ok;
            }
        }
    }
}