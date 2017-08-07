using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Attributes;
using Gigya.Common.Contracts.HttpService;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    [HttpService(7555)]
    public interface ISlowService
    {
        [Cached] Task<int> SimpleSlowMethod(int id, int millisecondsDelay, bool shouldThrow = false);
        [Cached] Task<IEnumerable<SlowData>> ComplexSlowMethod(int millisecondsDelay, IEnumerable<SlowData> slowDatas, bool shouldThrow = false);

        Task<int> SimpleSlowMethodUncached(int id, int millisecondsDelay, bool shouldThrow = false);
        Task<IEnumerable<SlowData>> ComplexSlowMethodUncached(int millisecondsDelay, IEnumerable<SlowData> slowDatas, bool shouldThrow = false);
    }

    public delegate Task<int> SimpleDelegate(int id, int millisecondsDelay, bool shouldThrow = false);
    public delegate Task<IEnumerable<SlowData>> ComplexDelegate(int millisecondsDelay, IEnumerable<SlowData> slowDatas, bool shouldThrow = false);

    public class SlowData
    {
        public int SerialNumber { get; set; }
        public string LuckyString { get; set; } = "Leprechaun";
        public DateTime TimeCreatedUtc { get; set; } = DateTime.Today;
    }
}
