using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gigya.Microdot.UnitTests.Caching.Host
{
    public class SlowService : ISlowService
    {
        private int counter;

        public async Task<int> SimpleSlowMethod(int id, int millisecondsDelay, bool shouldThrow)
        {
            await Task.Delay(millisecondsDelay);

            if (shouldThrow)
                throw new Exception(Interlocked.Increment(ref counter).ToString());

            return Interlocked.Increment(ref counter);
        }


        public async Task<IEnumerable<SlowData>> ComplexSlowMethod(int millisecondsDelay, IEnumerable<SlowData> slowDatas, bool shouldThrow)
        {
            await Task.Delay(millisecondsDelay);

            if (shouldThrow)
                throw new Exception(Interlocked.Increment(ref counter).ToString());

            var now = DateTime.UtcNow;
            var datas = slowDatas.ToArray();
            var serialNumber = Interlocked.Increment(ref counter);

            foreach (var data in datas)
            {
                data.TimeCreatedUtc = now;
                data.SerialNumber = serialNumber;
            }

            return datas;
        }


        public Task<int> SimpleSlowMethodUncached(int id, int millisecondsDelay, bool shouldThrow)
        {
            return SimpleSlowMethod(id, millisecondsDelay, shouldThrow);
        }


        public Task<IEnumerable<SlowData>> ComplexSlowMethodUncached(int millisecondsDelay, IEnumerable<SlowData> slowDatas, bool shouldThrow)
        {
            return ComplexSlowMethod(millisecondsDelay, slowDatas, shouldThrow);
        }
    }
}
