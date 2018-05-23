using System;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    internal class NodeMonitoringState
    {
        private const double FirstAttemptDelaySeconds = 0.001;
        private const double MaxAttemptDelaySeconds = 2;
        private const double DelayMultiplier = 2;

        private Action ReachabilityChanged { get; }
        private ILog Log { get; }
        private int AttemptCount { get; set; }
        private DateTime StartTime { get; set; }
        private TimeSpan NextDelay { get; set; }
        public Exception LastException { get; private set; }
        public bool IsReachable { get; private set; } = true;
        private Task MonitoringTask { get; set; } = Task.FromResult(1);
        private CancellationTokenSource CancellationSource { get; set; }
        public Node Node { get; }
        private string DeploymentIdentifier { get; }
        private ReachabilityCheck ReachabilityCheck { get; }

        private readonly object _lock = new object();

        public NodeMonitoringState(Node node, string deploymentIdentifier, ReachabilityCheck reachabilityCheck, Action reachabilityChanged, ILog log)
        {
            Node = node;
            DeploymentIdentifier = deploymentIdentifier;
            ReachabilityCheck = reachabilityCheck;
            ReachabilityChanged = reachabilityChanged;
            Log = log;
        }

        public void ReportUnreachable(Exception ex)
        {
            LastException = ex;

            var reachabilityChanged = false;
            lock (_lock)
            {
                if (!MonitoringTask.IsCompleted)
                    return;

                if (IsReachable)
                {
                    Log.Info(_ => _("Node has become unreachable", unencryptedTags: NodeUnencryptedTags()));
                    reachabilityChanged = true;
                }

                IsReachable = false;
                
                CancellationSource = new CancellationTokenSource();

                // Task.Run is used here to have the long-running task of monitoring run on the thread pool,
                // otherwise it might prevent ASP.NET requests from completing or it might be tied to a specific
                // grain's lifetime, when it is actually a global concern.
                MonitoringTask = Task.Run(() => StartMonitoringNode(CancellationSource.Token));
            }
            // do it here to avoid calling an external delegate from inside a lock (which may cause a dead-lock)
            if (reachabilityChanged)
                ReachabilityChanged();

        }

        private async Task StartMonitoringNode(CancellationToken cancellationToken)
        {

            AttemptCount = 0;
            StartTime = DateTime.UtcNow;
            NextDelay = TimeSpan.FromSeconds(FirstAttemptDelaySeconds);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    AttemptCount++;

                    await ReachabilityCheck(Node, cancellationToken).ConfigureAwait(false);

                    // if ReachabilityCheck passed successfully, then service is reachable
                    IsReachable = true;
                    ReachabilityChanged();
                    Log.Info(_ => _("Node has become reachable", unencryptedTags: NodeUnencryptedTags()));
                    return;
                }
                catch (Exception ex)
                {
                    LastException = ex;
                    Log.Info(_ => _("A remote host is still unreachable, monitoring continues.", exception: ex, unencryptedTags: NodeUnencryptedTags()));
                }

                await Task.Delay(NextDelay, CancellationSource.Token).ConfigureAwait(false);

                NextDelay = TimeSpan.FromMilliseconds(NextDelay.TotalMilliseconds * DelayMultiplier);

                if (NextDelay.TotalSeconds > MaxAttemptDelaySeconds)
                    NextDelay = TimeSpan.FromSeconds(MaxAttemptDelaySeconds);
            }
        }

        public void StopMonitoring()
        {
            lock (_lock)
                CancellationSource?.Cancel();
        }

        private object NodeUnencryptedTags() => new
        {
            deploymentIdentifier = DeploymentIdentifier,
            hostname = Node.Hostname,
            port = Node.Port,
            attemptCount = AttemptCount,
            nextDelay = NextDelay,
            nextAttemptAt = DateTime.UtcNow + NextDelay,
            downtime = DateTime.UtcNow - StartTime
        };


    }
}