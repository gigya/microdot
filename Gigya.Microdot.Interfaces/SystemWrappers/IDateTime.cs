using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.Interfaces.SystemWrappers
{
    public interface IDateTime
    {
        DateTime UtcNow { get; }
        Task Delay(TimeSpan delay);
    }
}
