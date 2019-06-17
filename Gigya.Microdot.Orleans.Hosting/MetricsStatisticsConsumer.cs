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
        private readonly ConcurrentDictionary<string, double> _latestMetricValues = new ConcurrentDictionary<string, double>();
        private readonly Unit _totalMillisecondsUnit = Unit.Custom("TotalMilliseconds");
        private readonly MetricsContext _context = Metrics.Metric.Context("Silo");

        public void IncrementMetric(string name)
        {
            IncrementGauge(name, 1);
        }

        public void IncrementMetric(string name, double value)
        {
            IncrementGauge(name, value);
        }

        public void DecrementMetric(string name)
        {
            IncrementGauge(name, -1);
        }

        public void DecrementMetric(string name, double value)
        {
            IncrementGauge(name, -1 * value);
        }

        private void IncrementGauge(string name, double value)
        {
            //Increment Gauge by value
            UpdateGauge(name, value, (k, v) => v + value, Unit.None);
        }

        public void TrackMetric(string name, double value, IDictionary<string, string> properties = null)
        {
            //Override Gauge to last value
            UpdateGauge(name, value, (k, v) => value, Unit.None);
        }

        public void TrackMetric(string name, TimeSpan value, IDictionary<string, string> properties = null)
        {
            UpdateGauge(name, value.TotalMilliseconds, (k, v) => value.TotalMilliseconds, _totalMillisecondsUnit);
        }

        private void UpdateGauge(string name, double firstTimeValue, Func<string, double, double> updateValueFactory, Unit unit)
        {
            bool exists = true;

            _latestMetricValues.AddOrUpdate(name, key =>
            {
                exists = false;

                return firstTimeValue;
            }, updateValueFactory);

            // New counter discovered
            if (!exists)
                _context.Gauge(name, () => _latestMetricValues[name], unit);
        }

        public void Flush()
        {
        }

        public void Close()
        {
            _context.Dispose();
        }
    }
}