using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.SharedLogic;
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

            _testingKernel = new StandardKernel();
            _testingKernel.Bind<ConfigCreatorObject>().ToSelf().InTransientScope();
            _testingKernel.Bind<ConfigCreatorFuncObject>().ToSelf().InTransientScope();
            _testingKernel.Load<MicrodotModule>();
            _testingKernel.Load<NLogModule>();
            _testingKernel.Get<CurrentApplicationInfo>().Init("CalculatorService.Client");
            _testingKernel.Get<Ninject.SystemInitializer.SystemInitializer>().Init();
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
