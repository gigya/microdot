using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public interface IWorkloadMetrics : IDisposable
    {
        void Init();
    }
}
