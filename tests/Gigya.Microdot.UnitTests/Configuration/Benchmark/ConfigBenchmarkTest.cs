using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Ninject;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.Configuration.Benchmark
{
    public class ConfigBenchmarkTest
    {
        private IKernel _testingKernel;


        [OneTimeSetUp]
        public void SetUp()
        {

 
            MicrodotInitializer microdotInitializer = new MicrodotInitializer(
                "",
                new NLogModule(), kernel =>
            {
            });
            _testingKernel = microdotInitializer.Kernel;
        }

        [TearDown]
        public void Teardown()
        {
            _testingKernel.Dispose();
        }

        [Test]
        public void ConfigCreatorFuncObjectEvaluationBenchmark()
        {
            int magicNumber = 2000000;
            int maxTimeInSec = 1;

            ParallelOptions pOptions = new ParallelOptions();
            pOptions.MaxDegreeOfParallelism = 4;

            ConfigCreatorFuncObject configFunc = _testingKernel.Get<ConfigCreatorFuncObject>();
            configFunc.GetConfig()();

            EvaluateFunc(configFunc.GetConfig(), magicNumber, pOptions, maxTimeInSec);
        }

        private void EvaluateFunc<T>(Func<T> func, int count, ParallelOptions pOptions, int maxTimeInSec)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, count, pOptions, i => func());

            sw.Stop();

            Console.WriteLine($"Function was evaluated {count} times in {sw.Elapsed.TotalSeconds} seconds");

            Assert.Less(sw.Elapsed.TotalSeconds, maxTimeInSec);
        }
    }
}
