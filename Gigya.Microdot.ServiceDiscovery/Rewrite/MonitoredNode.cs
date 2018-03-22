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
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// A node monitored by LoadBalancer
    /// </summary>
    public class MonitoredNode : Node, IDisposable
    {
        private const double FirstAttemptDelaySeconds = 0.001;
        private const double MaxAttemptDelaySeconds  = 10;
        private const double DelayMultiplier = 2;

        private ReachabilityCheck ReachabilityCheck { get; }
        private IDateTime DateTime { get; }
        private ILog Log { get; }
        private readonly string _serviceName;

        private readonly object _lock = new object();

        public MonitoredNode(INode node, string serviceName, ReachabilityCheck reachabilityCheck, IDateTime dateTime, ILog log) : base(node.Hostname, node.Port)
        {
            _serviceName = serviceName;
            ReachabilityCheck = reachabilityCheck;
            DateTime = dateTime;
            Log = log;            
        }

        public void ReportReachable()
        {
            IsReachable = true;            
            StopMonitoring();
        }

        public Exception LastException { get; private set; }

        public void ReportUnreachable(Exception ex = null)
        {
            IsReachable = false;
            LastException = ex;

            // Task.Run is used here to have the long-running task of monitoring run on the thread pool,
            // otherwise it might prevent ASP.NET requests from completing or it might be tied to a specific
            // grain's lifetime, when it is actually a global concern.
            Task.Run(StartMonitoring);            
        }

        public bool IsReachable { get; private set; } = true;

        private bool IsMonitoring { get; set; }

        private async Task StartMonitoring()
        {
            lock (_lock)
            {
                if (IsMonitoring)
                    return;

                IsMonitoring = true;
            }
            
            var start = DateTime.UtcNow;
            var nextDelay = TimeSpan.FromSeconds(FirstAttemptDelaySeconds);
            var maxDelay = TimeSpan.FromSeconds(MaxAttemptDelaySeconds);
            var attemptCount = 1;

            while (true)
            {
                if (IsMonitoring == false)
                    return;

                try
                {
                    attemptCount++;

                    if (await ReachabilityCheck.Invoke(this).ConfigureAwait(false))
                        break;

                }
                catch (Exception ex)
                {
                    Log.Error(_ => _("The supplied reachability checker threw an exception while checking a remote host. See tags and inner exception for details.",
                        exception: ex,
                        unencryptedTags: new
                        {
                            serviceName = _serviceName,
                            Hostname,
                            Port
                        }));
                }

                if (IsMonitoring == false)
                    return;

                Log.Info(_ => _("A remote host is still unreachable, monitoring continues. See tags for details", unencryptedTags: new
                {
                    serviceName = _serviceName,
                    Hostname,
                    Port,
                    attemptCount,
                    nextDelay,                    
                    nextAttemptAt = DateTime.UtcNow + nextDelay,
                    downtime = DateTime.UtcNow - start
                }));

                await Task.Delay(nextDelay).ConfigureAwait(false);

                nextDelay = TimeSpan.FromMilliseconds(nextDelay.TotalMilliseconds * DelayMultiplier);

                if (nextDelay > maxDelay)
                    nextDelay = maxDelay;
            }

            lock (_lock)
            {
                IsMonitoring = false;
                IsReachable = true;

                Log.Info(_ => _("A remote host has become reachable. See tags for details.",
                        unencryptedTags: new
                        {
                            serviceName = _serviceName,
                            Hostname,
                            Port,
                            attemptCount,
                            downtime = DateTime.UtcNow - start
                        }));
            }
        }

        internal void StopMonitoring()
        {
            IsMonitoring = false;
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}