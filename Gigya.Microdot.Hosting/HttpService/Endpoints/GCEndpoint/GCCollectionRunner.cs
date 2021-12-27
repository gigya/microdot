using System;
using System.Diagnostics;
using System.Runtime;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public interface IGCCollectionRunner
    {
        GCCollectionResult Collect(GCType gcType);
    }
    
    public class GCCollectionRunner : IGCCollectionRunner
    {
        public GCCollectionResult Collect(GCType gcType)
        {
            var sp = Stopwatch.StartNew();
            var totalMemoryBeforeGC = System.GC.GetTotalMemory(false);

            switch (gcType)
            {
                case GCType.Gen0:
                    System.GC.Collect(0, GCCollectionMode.Forced);
                    break;
                case GCType.Gen1:
                    System.GC.Collect(1, GCCollectionMode.Forced);
                    break;
                case GCType.Gen2:
                    System.GC.Collect(2, GCCollectionMode.Forced);
                    break;
                case GCType.LOHCompaction:
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    System.GC.Collect(2, GCCollectionMode.Forced,false, true);
                    break;
                case GCType.BlockingLohCompaction:
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    System.GC.Collect(2, GCCollectionMode.Forced,true, true);
                    break;
                default:
                    throw new ArgumentException("GCType");
            }
                
            var totalMemoryAfterGc = System.GC.GetTotalMemory(false);

            return new GCCollectionResult(
                totalMemoryBeforeGc: totalMemoryBeforeGC,
                totalMemoryAfterGc: totalMemoryAfterGc,
                elapsedMilliseconds: sp.ElapsedMilliseconds
            );
        }
    }
}