using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Holds current state of a service as we see it on Consul
    /// </summary>
    public sealed class ConsulServiceState: IDisposable
    {
        private string _activeVersion;
        private Node[] _nodesOfAllVersions = new Node[0];

        /// <summary>
        /// Initialize a new Consul service
        /// </summary>
        public ConsulServiceState(string serviceName)
        {
            ServiceName = serviceName;
            ServiceNameOrigin = serviceName;
        }

        /// <summary>
        /// Service name as it was requested by code
        /// </summary>
        public string ServiceNameOrigin { get; }

        /// <summary>
        /// Service name as it appears on Consul (may be different by upper/lower case)
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Current known list of nodes for this service and this version
        /// </summary>
        public INode[] Nodes { get; set; } = new INode[0];

        /// <summary>
        /// Current active version of this service
        /// </summary>
        public string ActiveVersion
        {
            get => _activeVersion;
            set
            {
                _activeVersion = value;
                SetNodesByActiveVersion();
            }
        }

        /// <summary>
        /// All nodes for this service on Consul. Contains nodes of this service of any version
        /// </summary>
        public Node[] NodesOfAllVersions
        {
            get => _nodesOfAllVersions;
            set
            {
                _nodesOfAllVersions = value;
                SetNodesByActiveVersion();
            }
        }

        private void SetNodesByActiveVersion()
        {
            if (ActiveVersion == null)
                Nodes = NodesOfAllVersions;
            else
                Nodes = NodesOfAllVersions.Where(n => n.Version == ActiveVersion).ToArray();
        }

        /// <summary>
        /// Whether this service appears on Consul as a deployed service
        /// </summary>
        public bool IsDeployed { get; set; } = true;


        /// <summary>
        /// Last result received from Consul. Used for logging.
        /// </summary>
        public ConsulResult LastResult { get; set; }

        /// <summary>
        /// Current ModifyIndex of Health api on Consul
        /// </summary>
        public ulong HealthModifyIndex { get; set; } = 0;

        /// <summary>
        /// Current ModifyIndex of version api on Consul
        /// </summary>
        public ulong VersionModifyIndex { get; set; } = 0;

        /// <summary>
        /// A task for loading the service nodes
        /// </summary>
        public Task NodesLoading { get; set; }

        /// <summary>
        /// A task for loading the service version
        /// </summary>
        public Task VersionLoading { get; set; }
        
        #region ShutdownToken
        private readonly CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        /// <summary>
        /// CancellationToken which is cancelled when this service is no longer needed
        /// </summary>
        public CancellationToken ShutdownToken => _shutdownTokenSource.Token;
        #endregion


        #region LongPollingToken
        private readonly object _longPollingTokenLocker = new object();
        private CancellationTokenSource _longPollingTokenSource = new CancellationTokenSource();
        /// <summary>
        /// CancellationToken which is cancelled when need to stop LongPolling in the middle
        /// </summary>

        public CancellationToken LongPollingToken => _longPollingTokenSource.Token;
        /// <summary>
        /// Stop the Long-Polling process in the middle by canceling its cancellationToken
        /// </summary>
        public void StopLongPolling()
        {
            lock (_longPollingTokenLocker)
            {
                var oldTokenSource = _longPollingTokenSource;
                _longPollingTokenSource = new CancellationTokenSource();
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
        }
        #endregion



        private int _disposed = 0;
        /// <summary>
        /// Cancel this service. It is no longer needed
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;

            _shutdownTokenSource.Cancel();            
            _shutdownTokenSource.Dispose();
            _longPollingTokenSource.Cancel();
            _longPollingTokenSource.Dispose();
        }
    }
}