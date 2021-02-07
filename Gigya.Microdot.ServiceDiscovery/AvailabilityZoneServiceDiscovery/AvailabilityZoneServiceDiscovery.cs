using Gigya.Common.Contracts.Exceptions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.ServiceDiscovery.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public class AvailabilityZoneServiceDiscovery : IAvailabilityZoneServiceDiscovery
    {
        public AvailabilityZoneInfo Info { get; private set; } = new AvailabilityZoneInfo();
        public Task GetInitialReadZonesTcs() => _initialReadZonesTask.Task;


        private readonly CancellationToken _disposeCancellationToken;
        private readonly IDiscovery _discovery;
        private readonly Rewrite.IConsulClient _consulClient;
        private readonly TaskCompletionSource<bool> _initialReadZonesTask = new TaskCompletionSource<bool>();

        public AvailabilityZoneServiceDiscovery(
            string serviceName,
            CancellationToken ctk, IDiscovery discovery,
            Rewrite.IConsulClient consulClient)
        {
            Info.ServiceName = serviceName;
            _disposeCancellationToken = ctk;
            _discovery = discovery;
            _consulClient = consulClient;
            Task.Run(UpdateAndMonitorAvailabilityZoneAsync, _disposeCancellationToken);
        }

        private Tags GetUnencryptedTags(Dictionary<string, string> additionalKeys = null)
        {
            var tags = new Tags
            {
                {"AvailabilityInfoServiceName", Info.ServiceName},
                {"AvailabilityInfoServiceZone", Info.ServiceZone},
                {"AvailabilityInfoStatusCode", Info.StatusCode.ToString()},
                {"AvailabilityDeploymentServiceName", Info.DeploymentIdentifier?.ServiceName},
                {"AvailabilityDeploymentZone", Info.DeploymentIdentifier?.Zone},
            };

            if (additionalKeys != null)
            {
                foreach (var k in additionalKeys)
                {
                    tags.Add(k.Key, k.Value);
                }
            }

            return tags;
        }

        private async Task UpdateAndMonitorAvailabilityZoneAsync()
        {
            void SetInfoStatus(AvailabilityZoneInfo.StatusCodes statusCode, string errorMessage, string consulZoneSettingsKey, Exception innerException)
            {
                var tags = GetUnencryptedTags(new Dictionary<string, string> { { "ConsulZoneSettingsKey", consulZoneSettingsKey } });
                Info.StatusCode = statusCode;
                Info.Exception = new EnvironmentException(errorMessage, innerException, unencrypted: tags);
            }

            const string folder = "ZoneSettings";
            ulong modifyIndex = 0;

            SetInfoStatus(AvailabilityZoneInfo.StatusCodes.InitializingConnectionToConsul,
                "Initialize connection to consul",
                $"{folder}/{Info.ServiceName}", null);

            while (_disposeCancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    ConsulResponse<DbKeyValue> response = null;
                    bool exceptionThrown = false;
                    try
                    {
                        response = await _consulClient.GetKey<DbKeyValue>(modifyIndex, folder, Info.ServiceName, _disposeCancellationToken);
                    }
                    catch (Exception e)
                    {
                        SetInfoStatus(AvailabilityZoneInfo.StatusCodes.FailedOrInvalidKeyFromConsul,
                            "Failed to get key or Invalid key",
                            $"{folder}/{Info.ServiceName}", e);
                        exceptionThrown = true;
                    }

                    if (exceptionThrown)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), _disposeCancellationToken);
                        continue;
                    }

                    if (response == null)
                    {
                        SetInfoStatus(AvailabilityZoneInfo.StatusCodes.FailedConnectToConsul,
                            $"Failed to connect to consul. Service Unavailable{Environment.NewLine}Keeping activity on previous valid deployment identifier cluster",
                            $"{folder}/{Info.ServiceName}", null);
                    }
                    else if (response.StatusCode == null ||
                             response.StatusCode >= HttpStatusCode.InternalServerError ||
                             response.StatusCode < HttpStatusCode.OK)
                    {
                        if (response.StatusCode == HttpStatusCode.Continue)
                        {
                            // TODO: handle continue
                        }

                        SetInfoStatus(AvailabilityZoneInfo.StatusCodes.ConsulInternalError,
                            $"Consul internal error. Service returned error status: '{(response.StatusCode?.ToString() ?? "NULL")}', error: '{response.ResponseContent}'{Environment.NewLine}Keeping activity on previous valid cluster",
                            $"{folder}/{Info.ServiceName}", null);
                    }
                    else if (response.Error != null)
                    {
                        SetInfoStatus(AvailabilityZoneInfo.StatusCodes.MissingOrInvalidKeyValue,
                            $"Missing or invalid key in consul. Error: {response.Error.Message}",
                            $"{folder}/{Info.ServiceName}", response.Error);
                    }
                    else
                    {
                        modifyIndex = response.ModifyIndex ?? 0;
                        await SetDeploymentIdentifierAsync(response.ResponseObject, _disposeCancellationToken);
                        
                        if (Info.StatusCode == AvailabilityZoneInfo.StatusCodes.Ok)
                            _initialReadZonesTask.TrySetResult(default);
                    }
                }
                catch (Exception e)
                {
                    SetInfoStatus(AvailabilityZoneInfo.StatusCodes.CriticalError,
                        $"Critical error while connecting to consul: {e.Message}",
                        $"{folder}/{Info.ServiceName}", e);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), _disposeCancellationToken);
            }
        }

        private async Task SetDeploymentIdentifierAsync(DbKeyValue keyValueFromConsul, CancellationToken ctk)
        {
            var activeZone = keyValueFromConsul.ServiceZone.ToLower();
            var activeConsulZone = keyValueFromConsul.ConsulZone.ToLower();

            Info.ServiceZone = activeZone;
            var componentNameCandidate = $"{Info.ServiceName}_{activeZone}";

            if (Info.DeploymentIdentifier?.ServiceName != componentNameCandidate ||
                Info.DeploymentIdentifier?.Zone != activeConsulZone ||
                Info.StatusCode != AvailabilityZoneInfo.StatusCodes.Ok)
            {
                var tempDeploymentIdentifier = new DeploymentIdentifier(componentNameCandidate,
                    deploymentEnvironment: null, activeConsulZone); // null because of microdot discovery bug
                var respNodes = await _consulClient.GetHealthyNodes(tempDeploymentIdentifier, modifyIndex: 0, ctk);
                if (respNodes.StatusCode != HttpStatusCode.OK || respNodes.ResponseObject.Length <= 0)
                {
                    if (Info.DeploymentIdentifier != null)
                    {
                        Info.Exception = new EnvironmentException($"Cannot get healthy nodes from {componentNameCandidate}\nKeeping activity on cluster {Info.DeploymentIdentifier?.ServiceName} Zone:{Info.DeploymentIdentifier?.Zone}");
                        Info.StatusCode = AvailabilityZoneInfo.StatusCodes.FailedGetHealthyNodes;
                    }
                    else
                    {
                        // Can't init DeploymentIdentifier on startup.. throw
                        Info.Exception = new EnvironmentException(
                            $"Cannot get healthy nodes from {componentNameCandidate}. Probably bad consul configuration. Check consul->KeyValue->ZoneSettings->{Info.ServiceName} for correct settings");
                        throw Info.Exception;
                    }
                }
                else
                {
                    Info.DeploymentIdentifier = tempDeploymentIdentifier;
                    Info.StatusCode = AvailabilityZoneInfo.StatusCodes.Ok;
                }
            }
        }


        public async Task<bool> HandleEnvironmentChangesAsync()
        {
            await _initialReadZonesTask.Task;

            var nodes = await _discovery.GetNodes(Info.DeploymentIdentifier);

            if (ReferenceEquals(nodes, Info.Nodes))
            {
                if (Info.StatusCode != AvailabilityZoneInfo.StatusCodes.FailedGetHealthyNodes)
                    return false;

                // will throw to avoid pointing to unhealthy node
                throw new EnvironmentException(
                    $"Failed to GetNodes via IDiscovery for service", unencrypted: GetUnencryptedTags());
            }
                
            var tempInfo = new AvailabilityZoneInfo()
            {
                DeploymentIdentifier = Info.DeploymentIdentifier,
                Exception = Info.Exception,
                Nodes = nodes,
                ServiceName = Info.ServiceName,
                StatusCode = Info.StatusCode,
                ServiceZone = Info.ServiceZone
            };

            try
            {
                if (nodes == null)
                {
                    // will throw to avoid pointing to unhealthy node
                    tempInfo.StatusCode = AvailabilityZoneInfo.StatusCodes.FailedGetHealthyNodes; // Set failed so next polling iteration will try SetDeploymentIdentifier
                    tempInfo.Exception = new EnvironmentException($"Discovery GetNodes returned null"); // This exception will be changed on next polling iteration

                    throw new EnvironmentException($"Failed to GetNodes via IDiscovery for service", unencrypted: GetUnencryptedTags());
                }


                if (tempInfo.Nodes.Length < 1)
                {
                    tempInfo.StatusCode = AvailabilityZoneInfo.StatusCodes.FailedGetHealthyNodes; // Set failed so next polling iteration will try SetDeploymentIdentifier
                    tempInfo.Exception = new EnvironmentException($"Discovery GetNodes returned zero nodes (empty list)"); // This exception will be changed on next polling iteration

                    throw new EnvironmentException($"Failed to discover nodes. No nodes discovered - empty list returned", unencrypted: GetUnencryptedTags());
                }
            }
            finally
            {
                Info = tempInfo;
            }

            return true;
        }
    }
}