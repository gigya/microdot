namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public class Counter
    {
        public string EventName { get; set; }
        public string PerformanceCounterName { get; set; }
        public double? Value { get; set; }

        public Counter()
        {
            
        }

        public Counter(string eventName, string performanceCounterName)
        {
            EventName = eventName;
            PerformanceCounterName = performanceCounterName;
        }
    }
}
