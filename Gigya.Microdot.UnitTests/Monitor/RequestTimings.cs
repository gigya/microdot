using System;
using System.Linq;
using System.Threading;
using Gigya.Microdot.SharedLogic.Measurement;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests
{

    [TestFixture]

    public class RequestTimingsTests
    {


        // check we got less than 10% measurement error in heavily threaded scenarios
        [Test, Ignore("Not reliable")]
        public void TestConcurrency()
        {
            var stopwatch = new ConcurrentStopwatch();
            int predicted_total_ms = 0;
            // don't use parallel.foreach, we want context switches
            var threads = Enumerable.Range(0, 100).Select(i => new Thread(() => Work(i, stopwatch, ref predicted_total_ms))).ToArray();
            foreach (var thread in threads)
                thread.Start();
            foreach (var thread in threads)
                thread.Join();
            double delta = Math.Abs(predicted_total_ms - stopwatch.ElapsedMS.Value);
            Console.WriteLine("ConcurrentStopwatch error margin: {0:0.000}%", delta * 100 / predicted_total_ms);
            Assert.IsTrue(delta < 0.1 * predicted_total_ms);
        }


        static void Work(int thread_num, ConcurrentStopwatch stopwatch, ref int predicted_total_ms)
        {
            var rand = new Random(thread_num);
            for (int i = 0; i < 1000; ++i)
            {
                int delay_in_ms = rand.Next(10);
                Interlocked.Add(ref predicted_total_ms, delay_in_ms);
                using (stopwatch.Measure())
                    Thread.Sleep(delay_in_ms);
            }
        }


    }
}
