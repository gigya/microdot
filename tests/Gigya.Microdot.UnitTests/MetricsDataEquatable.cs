using Metrics;
using System.Collections.Generic;

namespace Gigya.Microdot.UnitTests
{
    public class MetricsDataEquatable
    {
        public MetricsDataEquatable()
        {
            Timers = new List<MetricDataEquatable>();
            TimersSettings = new MetricsCheckSetting { CheckValues = false };
            
            Counters = new List<MetricDataEquatable>();
            CountersSettings = new MetricsCheckSetting { CheckValues = false };
            
            Gauges = new List<MetricDataEquatable>();            
            GaugesSettings = new MetricsCheckSetting { CheckValues = false };

            Meters = new List<MetricDataEquatable>();
            MetersSettings = new MetricsCheckSetting { CheckValues = false };
        }
        
        public List<MetricDataEquatable> Gauges { get; set; }

        public MetricsCheckSetting GaugesSettings { get; set; }

        public List<MetricDataEquatable> Timers { get; set; }

        public MetricsCheckSetting TimersSettings { get; set; }

        public List<MetricDataEquatable> Counters { get; set; }

        public MetricsCheckSetting CountersSettings { get; set; }


        public List<MetricDataEquatable> Meters { get; set; }

        public MetricsCheckSetting MetersSettings { get; set; }

    }

    public class MetricsCheckSetting
    {
        public bool CheckValues { get; set; }        
    }

    public class MetricDataEquatable
    {
        public string Name { get; set; }

        public Unit Unit { get; set; }
        
        public long Value { get; set; }

        public string[] SubCounters { get; set; }
    }
}