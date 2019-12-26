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

namespace Gigya.Microdot.SharedLogic.Measurement
{
    public sealed class CustomTimings:IDisposable
    {
        readonly Stopwatch Stopwatch;


        public CustomTimings()
        {
            
        }

        private CustomTimings(string groupName, string measurementName)
        {
            GroupName=groupName;
            MeasurementName = measurementName;
            Stopwatch = Stopwatch.StartNew();
        }


        private string MeasurementName { get;  }


        private string GroupName { get; }

        /// <summary>
        /// This static used to measure via using pattern like: using(Report(groupName,measurementName))
        /// </summary>                
        public static CustomTimings Report(string groupName, string measurementName)
        {
            return new CustomTimings(groupName, measurementName);
        }

        public void Report(string groupName, string measurementName, TimeSpan value)
        {
            if(groupName == null)
                throw new ArgumentNullException(nameof(groupName));

            if (measurementName == null)
                throw new ArgumentNullException(nameof(measurementName));

            RequestTimings.Current.UserStats.AddOrUpdate(groupName+"."+measurementName, 
                new Aggregator {TotalInstances = 1, TotalTime = value},
                (key, aggregator) => {
                                        aggregator.TotalInstances++;
                                        aggregator.TotalTime = aggregator.TotalTime.Add(value);
                                        return aggregator;
                });
        }


        public void Dispose()
        {
            if(Stopwatch != null)
            {
                Stopwatch.Stop();
                Report(GroupName, MeasurementName, Stopwatch.Elapsed);
            }
        }
    }

    internal class Aggregator
    {
        public TimeSpan TotalTime { get; set; }
        public long TotalInstances { get; set; }
    }
}