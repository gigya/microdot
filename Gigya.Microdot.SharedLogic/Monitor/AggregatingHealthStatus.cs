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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Metrics;

namespace Gigya.Microdot.SharedLogic.Monitor
{
    public class AggregatingHealthStatus
    {
        private readonly List<DisposableHealthCheck> _checks = new List<DisposableHealthCheck>();
        private readonly object _locker = new object();

        public AggregatingHealthStatus(string componentName, IHealthMonitor healthMonitor)
        {
            healthMonitor.SetHealthFunction(componentName, HealthCheck);
        }

        private HealthCheckResult HealthCheck()
        {
            DisposableHealthCheck[] checks;
            lock (_locker)
            {
                checks = _checks.ToArray(); // get the current state of the health-checks list
            }

            // don't call the health check functions inside a lock. It may run for a long time, 
            // and in the worse case it may cause a dead-lock, if the function is locking something else that we depend on 
            var results = checks                
                .Select(c => new { c.Name, Result = c.CheckFunc() })
                .OrderBy(c => c.Result.IsHealthy)
                .ThenBy(c => c.Name)
                .ToArray();

            bool healthy = results.All(r => r.Result.IsHealthy);
            string message = string.Join(Environment.NewLine, results.Select(r => $"{(r.Result.IsHealthy ? "[OK]" : "[Unhealthy]")} {r.Name} - {r.Result.Message}"));

            return healthy ? HealthCheckResult.Healthy(message) : HealthCheckResult.Unhealthy(message);
        }

        public IDisposable RegisterCheck(string name, Func<HealthCheckResult> checkFunc)
        {
            lock (_locker)
            {
                var healthCheck = new DisposableHealthCheck(name, checkFunc, RemoveCheck);
                _checks.Add(healthCheck);
                return healthCheck;
            }
        }

        private void RemoveCheck(DisposableHealthCheck healthCheck)
        {
            lock (_locker)
            {
                _checks.Remove(healthCheck);
            }
        }

        private class DisposableHealthCheck : IDisposable
        {
            private readonly Action<DisposableHealthCheck> _disposed;
            public string Name { get; }
            public Func<HealthCheckResult> CheckFunc { get; private set; }

            public DisposableHealthCheck(string name, Func<HealthCheckResult> checkFunc, Action<DisposableHealthCheck> whenDisposed)
            {
                _disposed = whenDisposed;
                Name = name;
                CheckFunc = checkFunc;
            }

            public void Dispose()
            {                
                _disposed(this);
                CheckFunc = null;
            }
        }


    }

}