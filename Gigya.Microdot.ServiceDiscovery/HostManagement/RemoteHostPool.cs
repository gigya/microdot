#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;

namespace Gigya.Microdot.ServiceDiscovery.HostManagement
{
    /// <summary>
    /// A pool of remote hosts that provides round-robin load balancing and failure management.
    /// </summary>
    public sealed class RemoteHostPool : IDisposable
    {
        public ISourceBlock<ServiceReachabilityStatus> ReachabilitySource => ReachabilityBroadcaster;
        public  bool IsServiceDeploymentDefined => DiscoverySource.IsServiceDeploymentDefined;
        private readonly BroadcastBlock<EndPointsResult> _endPointsChanged = new BroadcastBlock<EndPointsResult>(null);
        public ISourceBlock<EndPointsResult> EndPointsChanged => _endPointsChanged;

        /// <summary>
        /// Time of the last attempt to reach the service.
        /// </summary>
        private DateTime LastEndpointRequest { get; set; }
        internal ServiceDiscoveryConfig GetConfig() => GetDiscoveryConfig().Services[DeploymentIdentifier.ServiceName];
        internal ReachabilityChecker ReachabilityChecker { get; }
        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }
        internal ILog Log { get; }
        internal DeploymentIdentifier DeploymentIdentifier { get; }

        private ulong Counter { get; set; }
        private ComponentHealthMonitor Health { get; }
        private List<RemoteHost> ReachableHosts { get; set; }
        private List<RemoteHost> UnreachableHosts { get; set; }
        private EndPointsResult EndPointsResult { get; set; }
        private EndPoint[] EndPoints => EndPointsResult?.EndPoints;

        private readonly object _lock = new object();
        private readonly Random _random = new Random();
        private TaskCompletionSource<RemoteHost> FirstAvailableHostCompletionSource { get; set; }
        private MetricsContext Metrics { get; }
        private IDisposable EndPointsChangedBlockLink { get; }
        private BroadcastBlock<ServiceReachabilityStatus> ReachabilityBroadcaster { get; }
        private IServiceDiscoverySource DiscoverySource { get; }


        /// <summary>
        /// Creates a new RemoteHostPool using the specified system name and liveliness checker.
        /// </summary>        
        /// <param name="reachabilityChecker">A delegate that checks if a given host is reachable or not. Used for background checks of unreachable hosts.
        /// Should return true if the host is reachable, or false if it is unreachable. It should not throw an exception.</param>
        /// <param name="log">An implementation of <see cref="ILog"/> used for logging.</param>
        public RemoteHostPool(
            DeploymentIdentifier deploymentIdentifier
            , IServiceDiscoverySource discovery
            , ReachabilityChecker reachabilityChecker
            , Func<DiscoveryConfig> getDiscoveryConfig
            , ILog log
            , HealthMonitor healthMonitor
            , MetricsContext metrics
        )
        {
            
            DiscoverySource = discovery;
            DeploymentIdentifier = deploymentIdentifier;
            ReachabilityChecker = reachabilityChecker;
            GetDiscoveryConfig = getDiscoveryConfig;
            Log = log;
            ReachabilityBroadcaster = new BroadcastBlock<ServiceReachabilityStatus>(null);
            Health = healthMonitor.Get(discovery.Deployment);
            Health.SetHealthData(HealthData);

            ReachableHosts = new List<RemoteHost>();
            UnreachableHosts = new List<RemoteHost>();
            EndPointsChangedBlockLink = discovery.EndPointsChanged.LinkTo(new ActionBlock<EndPointsResult>(_ => ReloadEndpoints(_)));
            ReloadEndpoints(discovery.Result);
            Metrics = metrics;
            var metricsContext = Metrics.Context(DiscoverySource.Deployment);
            metricsContext.Gauge("ReachableHosts", () => ReachableHosts.Count, Unit.Custom("EndPoints"));
            metricsContext.Gauge("UnreachableHosts", () => UnreachableHosts.Count, Unit.Custom("EndPoints"));
            
        }


        /// <summary>
        /// Loads the specified settings, overwriting existing settings.
        /// </summary>
        /// <param name="updatedEndPointsResult"></param>
        /// this <see cref="RemoteHostPool"/>.
        /// <exception cref="ArgumentNullException">Thrown when </exception>
        /// <exception cref="EnvironmentException"></exception>
        private void ReloadEndpoints(EndPointsResult updatedEndPointsResult)
        {
            lock (_lock)
            {
                try
                {
                    var updatedEndPoints = updatedEndPointsResult.EndPoints;
                    if (updatedEndPoints.Any() == false)
                    {
                        Health.SetHealthFunction(() =>
                        {
                            var config = GetConfig();
                            if (IsHealthCheckSuppressed(config))
                                return HealthCheckResult.Healthy(
                                    $"No endpoints were discovered from source '{config.Source}' but the remote service was not in use for more than {config.SuppressHealthCheckAfterServiceUnused.TotalSeconds} seconds.");
                            else
                                return HealthCheckResult.Unhealthy(
                                    $"No endpoints were discovered from source '{config.Source}'.");
                        });

                        EndPointsResult = updatedEndPointsResult;

                        ReachableHosts = new List<RemoteHost>();
                        UnreachableHosts = new List<RemoteHost>();
                    }
                    else
                    {
                        if (EndPoints != null)
                        {
                            foreach (var removedEndPoint in EndPoints.Except(updatedEndPoints))
                            {
                                ReachableHosts.SingleOrDefault(h => h.Equals(removedEndPoint))?.StopMonitoring();
                                ReachableHosts.RemoveAll(h => h.Equals(removedEndPoint));
                                UnreachableHosts.RemoveAll(h => h.Equals(removedEndPoint));
                            }
                        }

                        var newHosts = updatedEndPoints
                            .Except(EndPoints ?? Enumerable.Empty<EndPoint>())
                            .Select(ep => new RemoteHost(ep.HostName, this, _lock, ep.Port));

                        ReachableHosts.AddRange(newHosts);

                        EndPointsResult = updatedEndPointsResult;

                        Counter = (ulong)_random.Next(0, ReachableHosts.Count);

                        Health.SetHealthFunction(CheckHealth);
                    }

                    _endPointsChanged.Post(EndPointsResult);

                }
                catch (Exception ex)
                {
                    Log.Warn("Failed to process newly discovered endpoints", exception: ex);
                    Health.SetHealthFunction(() =>
                        HealthCheckResult.Unhealthy("Failed to process newly discovered endpoints: " +
                                                    HealthMonitor.GetMessages(ex)));
                }
            }
        }


        private Dictionary<string, string> HealthData()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>
                {
                    {"ReachableHosts", string.Join(",", ReachableHosts.Select(_ => _.HostName))},
                    {"UnreachableHosts", string.Join(",", UnreachableHosts.Select(_ => _.HostName))}
                };
            }
        }


        private bool IsHealthCheckSuppressed(ServiceDiscoveryConfig config)
        {
            var serviceUnuseTime = DateTime.UtcNow.Subtract(LastEndpointRequest);

            //If a service was unused for pre-defined time period, always treat it as healthy.
            if (serviceUnuseTime > config.SuppressHealthCheckAfterServiceUnused)
                return true;

            return false;
        }
        
        private HealthCheckResult CheckHealth()
        {
            var config = GetConfig();

            if (IsHealthCheckSuppressed(config))
                return HealthCheckResult.Healthy($"Health check suppressed because service was not in use for more than {config.SuppressHealthCheckAfterServiceUnused.TotalSeconds} seconds.");

            int reachableCount;
            int unreachableCount;
            Exception exception;

            string[] unreachableHosts;

            lock (_lock)
            {
                reachableCount = ReachableHosts.Count;
                unreachableHosts = UnreachableHosts.Select(x => $"{x.HostName}:{x.Port}").ToArray();
                unreachableCount = unreachableHosts.Length;
                exception = UnreachableHosts.FirstOrDefault()?.LastException;
            }

            if (reachableCount == 0)
            {
                return HealthCheckResult.Unhealthy($"All of the {unreachableCount} hosts are unreachable: " +
                    $"{string.Join(",", unreachableHosts)}. Last exception: {HealthMonitor.GetMessages(exception)}");
            }
            else
            {
                if (unreachableCount > 0)
                {
                    return HealthCheckResult.Unhealthy($"The following {unreachableCount} hosts " +
                        $"(out of {unreachableCount + reachableCount}) are unreachable: {string.Join(",", unreachableHosts)}. " +
                        $"Last exception: {HealthMonitor.GetMessages(exception)}");
                }
                else
                {
                    return HealthCheckResult.Healthy($"All {reachableCount} hosts are reachable.");
                }
            }
        }

        /// <summary>
        /// Retrieves the next reachable <see cref="RemoteHost"/>.
        /// </summary>
        /// <param name="affinityToken">
        /// A string to generate a consistent affinity to a specific host within the set of available hosts.
        /// Identical strings will return the same host for a given pool of reachable hosts. A request ID is usually provided.
        /// </param>
        /// <returns>A reachable <see cref="RemoteHost"/>.</returns>
        /// <exception cref="EnvironmentException">Thrown when there is no reachable <see cref="RemoteHost"/> available.</exception>
        public IEndPointHandle GetNextHost(string affinityToken = null)
        {
            LastEndpointRequest = DateTime.UtcNow;

            var hostOverride = TracingContext.GetHostOverride(DeploymentIdentifier.ServiceName);

            if (hostOverride != null)
                return new OverriddenRemoteHost(DeploymentIdentifier.ServiceName, hostOverride.Host, hostOverride.Port?? GetConfig().DefaultPort);

            lock (_lock)
            {
                Health.Activate();

                if (ReachableHosts.Count == 0)
                {
                    var lastExceptionEndPoint = UnreachableHosts.FirstOrDefault();

                    // TODO: Exception throwing code should be in this class, not in another.
                    throw DiscoverySource.AllEndpointsUnreachable(EndPointsResult, lastExceptionEndPoint?.LastException, lastExceptionEndPoint == null ? null : $"{lastExceptionEndPoint.HostName}:{lastExceptionEndPoint.Port}", string.Join(", ", UnreachableHosts));
                }

                Counter++;

                ulong hostId = affinityToken == null ? Counter : (ulong)affinityToken.GetHashCode();

                return ReachableHosts[(int)(hostId % (ulong)ReachableHosts.Count)];
            }
        }

        public async Task<IEndPointHandle> GetOrWaitForNextHost(CancellationToken cancellationToken)
        {
            var hostOverride = TracingContext.GetHostOverride(DeploymentIdentifier.ServiceName);

            if (hostOverride != null)
                return new OverriddenRemoteHost(DeploymentIdentifier.ServiceName, hostOverride.Host, hostOverride.Port ?? GetConfig().DefaultPort);

            if (ReachableHosts.Count > 0)
                return GetNextHost();

            lock (_lock)
            {
                if (FirstAvailableHostCompletionSource == null)
                    FirstAvailableHostCompletionSource = new TaskCompletionSource<RemoteHost>();

                cancellationToken.Register(() => FirstAvailableHostCompletionSource?.SetCanceled());
            }

            return await FirstAvailableHostCompletionSource.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Resets the state of all hosts as reachable.
        /// </summary>
        public void MarkAllAsReachable()
        {
            lock (_lock)
            {
                foreach (var unreachableHost in UnreachableHosts.ToArray())
                {
                    unreachableHost.ReportSuccess();
                    MarkReachable(unreachableHost);
                }
            }
        }

        internal bool MarkUnreachable(RemoteHost remoteHost)
        {
            lock (_lock)
            {
                if (ReachableHosts.Remove(remoteHost))
                {
                    if (ReachableHosts.Count == 0)
                        ReachabilityBroadcaster.Post(new ServiceReachabilityStatus { IsReachable = false });
                    UnreachableHosts.Add(remoteHost);
                    return true;
                }

                return false;
            }
        }

        internal bool MarkReachable(RemoteHost remoteHost)
        {
            lock (_lock)
            {
                if (UnreachableHosts.Remove(remoteHost))
                {
                    ReachableHosts.Add(remoteHost);
                    if (ReachableHosts.Count == 1)
                        ReachabilityBroadcaster.Post(new ServiceReachabilityStatus { IsReachable = true });

                    FirstAvailableHostCompletionSource?.SetResult(remoteHost);
                    FirstAvailableHostCompletionSource = null;

                    return true;
                }

                return false;
            }
        }

        internal string GetAllHosts()
        {
            lock (_lock)
            {
                return string.Join(", ", ReachableHosts.Concat(UnreachableHosts).Select(h => h.HostName));
            }
        }


        public void Dispose()
        {
            lock (_lock)
            {
                EndPointsChangedBlockLink.Dispose();
                foreach (var host in ReachableHosts.Concat(UnreachableHosts).ToArray())
                    host.StopMonitoring();
                ReachabilityBroadcaster.Complete();
                DiscoverySource.Dispose();
                Health.Dispose();
            }
        }


        public void DeactivateMetrics()
        {
            Health.Deactivate();
        }

        public EndPoint[] GetAllEndPoints() { return EndPointsResult.EndPoints; }
    }


    public interface IRemoteHostPoolFactory
    {
        RemoteHostPool Create(DeploymentIdentifier deploymentIdentifier, IServiceDiscoverySource discovery,
            ReachabilityChecker reachabilityChecker);
    }
}