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

#endregion Copyright

using Metrics;
using Orleans.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Gigya.Microdot.Orleans.Hosting
{

    public class MetricsStatisticsConsumer : IMetricTelemetryConsumer
    {
        private readonly ConcurrentDictionary<string, double> latestMetricValues = new ConcurrentDictionary<string, double>();
        private readonly ConcurrentDictionary<string, Timer> latestMetricTimers = new ConcurrentDictionary<string, Timer>();

        private readonly MetricsContext context = Metrics.Metric.Context("Silo");

        public void IncrementMetric(string name)
        {
            IncrementMetric(name, 1);
        }

        public void IncrementMetric(string name, double value)
        {
            TrackMetric(name, value);
        }

        public void DecrementMetric(string name)
        {
            DecrementMetric(name, 1);
        }

        public void DecrementMetric(string name, double value)
        {
            TrackMetric(name, -value);
        }

        //properties?
        public void TrackMetric(string name, double value, IDictionary<string, string> properties = null)
        {
            if (latestMetricValues.ContainsKey(name)) // new counter discovered
            { //need lock?
                context.Gauge(name, () => latestMetricValues[name], Unit.None);
            }

            latestMetricValues.AddOrUpdate(name, key => value, (k, v) => v + value);
        }

        public void TrackMetric(string name, TimeSpan value, IDictionary<string, string> properties = null)
        {
            latestMetricTimers.AddOrUpdate(name, (k) =>
            {
                var result = context.Timer(name, unit: Unit.None);
                result.Record((int)value.TotalMilliseconds, TimeUnit.Milliseconds);
                return result;
            }, (k, timer) =>
           {
               timer.Record((int)value.TotalMilliseconds, TimeUnit.Milliseconds);
               return timer;
           });
        }

        public void TrackDependency(string name, string commandName, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            return;
        }

        public void TrackEvent(string name, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            return;
        }

        public void TrackRequest(string name, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success)
        {
            return;
        }

        public void TrackException(Exception exception, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            return;
        }

        public void TrackTrace(string message)
        {
            return;
        }

        public void TrackTrace(string message, Severity severityLevel)
        {
            return;
        }

        public void TrackTrace(string message, Severity severityLevel, IDictionary<string, string> properties)
        {
            return;
        }

        public void TrackTrace(string message, IDictionary<string, string> properties)
        {
            return;
        }

        public void Flush()
        {
            return;
        }

        public void Close()
        {
            return;
        }
    }

}