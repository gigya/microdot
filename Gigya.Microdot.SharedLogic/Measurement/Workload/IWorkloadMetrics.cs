using System;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public interface IWorkloadMetrics : IDisposable
    {
        void Init();
    }
}
