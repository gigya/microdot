using System;
using System.Diagnostics;

namespace Gigya.Microdot.SharedLogic.Measurement
{
    public class CustomTimings:IDisposable
    {
        readonly Stopwatch Stopwatch;


        public CustomTimings()
        {
            
        }

        private CustomTimings(string groupName, string measurementName)
        {
            GroupName=groupName;
            MeasurementName = measurementName;
            Stopwatch = new Stopwatch();
            Stopwatch.Start();
        }


        protected string MeasurementName { get; set; }


        protected string GroupName { get; set; }

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