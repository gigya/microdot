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
using System.Diagnostics;
using System.Threading;

namespace Gigya.Microdot.SharedLogic.Measurement
{
    /// <summary>Enables multiple threads to start and stop time measurements. <see cref="ElapsedMS"/> returns the total
    /// past and ongoing time span measured. This class is lock-free, uses constant memory, and has a slight chance for
    /// inaccuracies due to race conditions.</summary>
    [Serializable]
    public class ConcurrentStopwatch: IMeasurement {

        /// <summary>The total number of measurements performed and in progress.</summary>
        long NumMeasurements;

        /// <summary>The number of measurements that are currently in progress.</summary>
        long NumMeasurementsInProgress;

        /// <summary>The timestamp (from Stopwatch.GetTimestamp()) at which the last measurement was started or ended.</summary>
        long LastTimestamp;

        /// <summary>The total number of ticks accumulated as of the last measurement that was started or ended.</summary>
        long ElapsedTicks;


        /// <summary>Starts measuring time. Can be called multiple times to perform multiple measurements. Try using the
        /// safer <see cref="Measure"/> method.</summary>
        /// 
        internal void Start() {
            Interlocked.Increment(ref NumMeasurements);
            long new_ts = Stopwatch.GetTimestamp();
            long old_ts = Interlocked.Exchange(ref LastTimestamp, new_ts);
            long concurrent_measurements = Interlocked.Increment(ref NumMeasurementsInProgress) - 1;
            if (old_ts > 0)
                Interlocked.Add(ref ElapsedTicks, (new_ts - old_ts) * concurrent_measurements);
        }


        /// <summary>Stops one ongoing time measurement. Call this as many times you called <see cref="Start"/>.</summary>
        /// 
        internal void Stop() {
            long new_ts = Stopwatch.GetTimestamp();
            long old_ts = Interlocked.Exchange(ref LastTimestamp, new_ts);
            long concurrent_measurements = Interlocked.Decrement(ref NumMeasurementsInProgress) + 1;
            Interlocked.Add(ref ElapsedTicks, (new_ts - old_ts) * concurrent_measurements);
        }


        /// <summary>Starts measuring time, and stops when you call the returned object's Dispose() method, as occurs in a
        /// "using" statement.</summary>
        /// 
        public IDisposable Measure() {
            Start();
            return new Closure { action = () => Stop() };
        }


        internal class Closure : IDisposable {
            public Action action;
            void IDisposable.Dispose() { action(); }
        }



        /// <summary>The total time measured across all measurements performed, including still-ongoing measurements.
        /// Null if no measurements were performed.</summary>
        /// 
        public double? ElapsedMS {
            get {
                if (NumMeasurements == 0)
                    return null;
                long ongoing_elapsed = (Stopwatch.GetTimestamp() - LastTimestamp) * NumMeasurementsInProgress;
                double total_elpased_ms = 1000.0 * (ElapsedTicks + ongoing_elapsed) / Stopwatch.Frequency;
                return Math.Round(total_elpased_ms, 3);
            }
        }

        /// <summary>The total number of measurements performed and in progress. Null if no measurements were performed.</summary>
        /// 
        public long? Calls { get { return NumMeasurements == 0 ? null : (long?)NumMeasurements; } }

    }
}