using System;
using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.SharedLogic.SystemWrappers
{
    public class DateTimeImpl: IDateTime
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public Task Delay(TimeSpan delay) { return Task.Delay(delay); }
    }
}
