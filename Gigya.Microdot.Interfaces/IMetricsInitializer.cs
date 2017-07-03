using System;

namespace Gigya.Microdot.Interfaces
{
    public interface IMetricsInitializer: IDisposable
    {
        void Init();
    }
}
