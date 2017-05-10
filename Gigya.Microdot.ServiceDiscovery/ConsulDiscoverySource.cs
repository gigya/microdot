using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;

namespace Gigya.Microdot.ServiceDiscovery
{
    /// <summary>
    /// Reads a list of endpoints from Consul based on the service name it was initialized with and continuously listens/
    /// polls Consul for endpoint changes.
    /// </summary>
    public class ConsulDiscoverySource : ServiceDiscoverySourceBase
    {
        public override Task InitCompleted => _initialized;
        public override bool IsServiceDeploymentDefined => _lastConsulResult?.IsQueryDefined ?? true;

        private IConsulClient ConsulClient { get; }
        private CancellationTokenSource ShutdownToken { get; }
        private readonly ServiceDiscoveryConfig _config;
        private readonly IDateTime _dateTime;
        private readonly ILog _log;
        private EndPointsResult _lastConsulResult;
        private readonly object _lastResultLocker = new object();
        private Task _initialized;
        private bool _firstTime = true;

        public ConsulDiscoverySource(ServiceDeployment serviceDeployment,
                                     ServiceDiscoveryConfig serviceDiscoveryConfig,
                                     IConsulClient consulClient,
                                     IDateTime dateTime, ILog log)
            : base(GetDeploymentName(serviceDeployment, serviceDiscoveryConfig))

        {
            ConsulClient = consulClient;
            _config = serviceDiscoveryConfig;
            _dateTime = dateTime;
            _log = log;
            ShutdownToken = new CancellationTokenSource();
            Task.Run(() => RefreshHostForever(ShutdownToken.Token));
            _initialized = Task.Run(Load); // Must be run in Task.Run() because of incorrect Orleans scheduling
        }

        private static string GetDeploymentName(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoverySettings)
        {
            if (serviceDiscoverySettings.Scope == ServiceScope.DataCenter)
            {
                return serviceDeployment.ServiceName;
            }
            return $"{serviceDeployment.ServiceName}-{serviceDeployment.DeploymentEnvironment}";
        }

        private async Task RefreshHostForever(CancellationToken shutdownToken)
        {
            while (shutdownToken.IsCancellationRequested == false)
            {
                try
                {
                    await Load().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _log.Critical("Failed to load endpoints from Consul", e);
                }

                await _dateTime.Delay(_config.ReloadInterval.Value).ConfigureAwait(false);
            }
        }

        private async Task Load()
        {
            var lastConsulResult = await ConsulClient.GetEndPoints(DeploymentName).ConfigureAwait(false);
            lock (_lastResultLocker)
            {
                if (lastConsulResult.Error != null)
                {
                    _lastConsulResult = lastConsulResult;
                    return;
                }
                var newEndPoints = lastConsulResult
                    .EndPoints                    
                    .OrderBy(x => x.HostName)
                    .ThenBy(x => x.Port)
                    .ToArray();

                if (newEndPoints.SequenceEqual(EndPoints) == false)
                {
                    EndPoints = newEndPoints;

                    _log.Info(_ => _("Obtained new list endpoints for service from Consul", unencryptedTags: new
                    {
                        serviceName = DeploymentName,
                        endpoints = string.Join(", ", EndPoints.Select(e => e.HostName + ':' + (e.Port?.ToString() ?? "")))
                    }));

                    if (!_firstTime || _lastConsulResult?.Error != null)
                        EndPointsChanged?.Post(lastConsulResult);
                }

                _firstTime = false;
                _lastConsulResult = lastConsulResult;
                _initialized = Task.FromResult(1);
            }
        }

        public override Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts)
        {
            if (endPointsResult.EndPoints.Length == 0)
            {
                var tags = new Tags
                {
                    {"requestedService", DeploymentName},
                    {"consulAddress", ConsulClient?.ConsulAddress?.ToString()},
                    { "requestTime", endPointsResult?.RequestDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                    { "requestLog", endPointsResult?.RequestLog },
                    { "responseLog", endPointsResult?.ResponseLog },
                    { "queryDefined", endPointsResult.IsQueryDefined.ToString() },
                    { "consulError", endPointsResult?.Error?.ToString() }
                };

                if (_lastConsulResult == null)
                    return new ProgrammaticException("Response not arrived from Consul. " +
                                                     "This should not happen, ConsulDiscoverySource should await until a result is returned from Consul.", unencrypted: tags);

                else if (endPointsResult.Error != null)
                    return new EnvironmentException("Error calling Consul. See tags for details.", unencrypted: tags);
                else if (endPointsResult.IsQueryDefined==false)
                    return new EnvironmentException("Query not exists on Consul. See tags for details.", unencrypted: tags);
                else
                    return new EnvironmentException("No endpoint were specified in Consul for the requested service.", unencrypted: tags);
            }
            else
            {
                return new MissingHostException("All endpoints defined by Consul for the requested service are unreachable. " +
                                                "Please make sure the endpoints on Consul are correct and are functioning properly. " +
                                                "See tags for the name of the requested service, and address of consul from which they were loaded. " +
                                                "exception for one of the causes a remote host was declared unreachable.",
                    lastException,
                    unencrypted: new Tags
                    {
                        {"consulAddress", ConsulClient.ConsulAddress.ToString()},
                        { "unreachableHosts", unreachableHosts },
                        {"requestedService", DeploymentName},
                        { "innerExceptionIsForEndPoint", lastExceptionEndPoint }
                    });
            }
        }

        public override void ShutDown()
        {
            ShutdownToken.Cancel();
        }
    }
}