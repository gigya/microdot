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
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// A node that exposes <see cref="IsReachable"/>=true until <see cref="ReportUnreachable"/> is called, at which time
    /// <see cref="IsReachable"/> returns false and the provided <see cref="ReachabilityCheck"/> is used to test the remote
    /// host using exponential backoff till the <see cref="ReachabilityCheck"/> returns true, or till
    /// <see cref="ReportReachable"/> was called, at which time <see cref="IsReachable"/> is set back to true.
    /// </summary>
    public class MonitoredNode : Node, IMonitoredNode
    {
        private const double FirstAttemptDelaySeconds = 0.001;
        private const double MaxAttemptDelaySeconds  = 2;
        private const double DelayMultiplier = 2;

        private ReachabilityCheck ReachabilityCheck { get; }
        private IDateTime DateTime { get; }
        private ILog Log { get; }
        private readonly string _serviceName;

        private readonly object _lock = new object();
        private Task _monitoringTask = Task.FromResult(1);
        private CancellationTokenSource _monitoringCancellationSource;
        private int _monitoringAttemptCount;
        private DateTime _monitoringStartTime;
        private TimeSpan _monitoringNextDelay;

        public MonitoredNode(INode node, string serviceName, ReachabilityCheck reachabilityCheck, IDateTime dateTime, ILog log) : base(node.Hostname, node.Port)
        {
            _serviceName = serviceName;
            ReachabilityCheck = reachabilityCheck;
            DateTime = dateTime;
            Log = log;            
        }

        public void ReportReachable()
        {
            // TODO: add metrics about reachable/unreachable nodes
        }

        public Exception LastException { get; private set; }

        public void ReportUnreachable(Exception ex = null)
        {
            
            LastException = ex;

            lock (_lock)
            {
                if (!_monitoringTask.IsCompleted)
                    return;

                if (IsReachable)
                   Log.Info(_ => _("Node has become unreachable", unencryptedTags: UnencryptedTags));


                IsReachable = false;
                _monitoringCancellationSource = new CancellationTokenSource();

                // Task.Run is used here to have the long-running task of monitoring run on the thread pool,
                // otherwise it might prevent ASP.NET requests from completing or it might be tied to a specific
                // grain's lifetime, when it is actually a global concern.
                _monitoringTask = Task.Run(()=>StartMonitoring(_monitoringCancellationSource.Token));                               
            }
        }

        public bool IsReachable { get; private set; } = true;
        
        private async Task StartMonitoring(CancellationToken cancellationToken)
        {
            _monitoringAttemptCount = 0;
            _monitoringStartTime = DateTime.UtcNow;
            _monitoringNextDelay = TimeSpan.FromSeconds(FirstAttemptDelaySeconds);            

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _monitoringAttemptCount++;

                    await ReachabilityCheck(this, cancellationToken).ConfigureAwait(false);
                   
                    // if ReachabilityCheck passed successfully, then service is reachable
                    lock (_lock)
                    {
                        IsReachable = true;
                        Log.Info(_ => _("Node has become reachable", unencryptedTags: UnencryptedTags));
                        return;
                    }                 
                }
                catch (Exception ex)
                {
                    LastException = ex;
                    Log.Info(_ => _("A remote host is still unreachable, monitoring continues.", exception: ex, unencryptedTags: UnencryptedTags));
                }

                await Task.Delay(_monitoringNextDelay, _monitoringCancellationSource.Token).ConfigureAwait(false);

                _monitoringNextDelay = TimeSpan.FromMilliseconds(_monitoringNextDelay.TotalMilliseconds * DelayMultiplier);

                if (_monitoringNextDelay.TotalSeconds > MaxAttemptDelaySeconds)
                    _monitoringNextDelay = TimeSpan.FromSeconds(MaxAttemptDelaySeconds);
            }
        }

        public void StopMonitoring()
        {
            lock (_lock)
                _monitoringCancellationSource?.Cancel();
        }

        private object UnencryptedTags => new
        {
            serviceName = _serviceName,
            hostname = Hostname,
            port = Port,
            attemptCount = _monitoringAttemptCount,
            nextDelay = _monitoringNextDelay,
            nextAttemptAt = DateTime.UtcNow + _monitoringNextDelay,
            downtime = DateTime.UtcNow - _monitoringStartTime
        };
    }
}