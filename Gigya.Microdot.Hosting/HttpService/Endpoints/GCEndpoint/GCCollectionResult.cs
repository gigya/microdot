namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public class GCCollectionResult
    {
        public long TotalMemoryBeforeGC { get; }
        public long TotalMemoryAfterGC { get; }
        public long ElapsedMilliseconds { get; }

        public GCCollectionResult(long totalMemoryBeforeGc, long totalMemoryAfterGc, long elapsedMilliseconds)
        {
            TotalMemoryBeforeGC = totalMemoryBeforeGc;
            TotalMemoryAfterGC = totalMemoryAfterGc;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }
}