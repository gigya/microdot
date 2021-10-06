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

using Metrics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Gigya.Microdot.SharedLogic.Monitor
{
    public sealed class HealthMonitor : IDisposable, IHealthMonitor
    {
        private static readonly ConcurrentDictionary<string, ComponentHealthMonitor> _componentMonitors = new ConcurrentDictionary<string, ComponentHealthMonitor>();

        public ComponentHealthMonitor SetHealthFunction(string component, 
            Func<HealthCheckResult> check, 
            Func<Dictionary<string, string>> healthData=null)
        {
            var componentHealthMonitor = _componentMonitors.GetOrAdd(component, _ => new ComponentHealthMonitor(component, check));
            componentHealthMonitor.Activate();
            componentHealthMonitor.SetHealthFunction(check);
            componentHealthMonitor.SetHealthData(healthData);
            return componentHealthMonitor;
        }

        public ComponentHealthMonitor Get(string component)
        {
            return _componentMonitors.GetOrAdd(component, _ => new ComponentHealthMonitor(component, HealthCheckResult.Healthy));
        }

        public void Dispose()
        {
             _componentMonitors.Clear();
             HealthChecks.UnregisterAllHealthChecks();
        }


        /// <summary>
        /// Return health data for specified component
        /// </summary>
        public Dictionary<string, string> GetData(string component)
        {
            if (_componentMonitors.ContainsKey(component))
                return _componentMonitors[component].GetHealthData();
            else
                return new Dictionary<string, string>();
        }

        public static string GetMessages(Exception ex)
        {
            var messages = new List<string>();
            var current = ex;
            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }
            return string.Join(" --> ", messages);
        }
    }

}
