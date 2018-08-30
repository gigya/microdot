using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ninject;

namespace CalculatorService.Client.ConfigCreationBenchmarkTest
{
    public class BenchmarkTest
    {
        private readonly IKernel _kernel;

        public BenchmarkTest(IKernel kernel)
        {
            _kernel = kernel;
            _kernel.Bind<ConfigCreatorTestObject>().ToSelf().InTransientScope();
            _kernel.Bind<ConfigCreatorTestFuncObject>().ToSelf().InTransientScope();
        }

        public void RunBenchmark()
        {
            Console.WriteLine("Resolving test...");

            ParallelOptions pOptions = new ParallelOptions();
            pOptions.MaxDegreeOfParallelism = 4;

            RunObjectCreationTest<ConfigCreatorTestObject>(_kernel, 2000000, pOptions);
            RunObjectCreationTest<ConfigCreatorTestFuncObject>(_kernel, 2000000, pOptions);

            ConfigCreatorTestFuncObject testClass = _kernel.Get<ConfigCreatorTestFuncObject>();
            testClass.GetConfig();

            EvaluateFunc(testClass.GetConfig(), 2000000, pOptions);

            Console.ReadLine();

        }

        private void RunObjectCreationTest<T>(IKernel kernel, int count, ParallelOptions pOptions)
        {
            Console.WriteLine($"Start resolving {count} {typeof(T).BaseType.GetGenericArguments()[0].FullName}");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, count, pOptions, i => kernel.Get<T>());

            sw.Stop();

            Console.WriteLine($"{count} objects created in {sw.Elapsed.TotalSeconds} seconds");
            Console.WriteLine();
        }

        private void EvaluateFunc<T>(Func<T> func, int count, ParallelOptions pOptions)
        {
            Console.WriteLine($"Start invoking {count} times");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, count, pOptions, i => func());

            sw.Stop();

            Console.WriteLine($"Function was evaluated {count} times in {sw.Elapsed.TotalSeconds} seconds");
        }
    }
}
