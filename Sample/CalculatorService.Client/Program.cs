using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CalculatorService.Interface;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace CalculatorService.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", Environment.CurrentDirectory);
                Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", "");
                Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", Environment.CurrentDirectory);
                Environment.SetEnvironmentVariable("DC", "global");
                Environment.SetEnvironmentVariable("ENV", "dev");

                CurrentApplicationInfo.Init("CalculatorService.Client");

                var kernel = new StandardKernel();
                kernel.Load<MicrodotModule>();
                kernel.Load<NLogModule>();

                //kernel.Bind<ConfigCreatorTestObject>().ToSelf().InTransientScope();
                //kernel.Bind<ConfigCreatorTestFuncObject>().ToSelf().InTransientScope();
                //kernel.Bind<ConfigCreatorTestISourceBlockObject>().ToSelf().InTransientScope();
                //kernel.Bind<ConfigCreatorTestFuncISourceBlockObject>().ToSelf().InTransientScope();

                //Console.WriteLine("Resolving test...");

                //ParallelOptions pOptions = new ParallelOptions();
                //pOptions.MaxDegreeOfParallelism = 4;

                //RunObjectCreationTest<ConfigCreatorTestObject>(kernel, 2000000, pOptions);
                //RunObjectCreationTest<ConfigCreatorTestFuncObject>(kernel, 2000000, pOptions);
                ////RunObjectCreationTest<ConfigCreatorTestISourceBlockObject>(kernel, 2000000, pOptions);
                ////RunObjectCreationTest<ConfigCreatorTestFuncISourceBlockObject>(kernel, 2000000, pOptions);

                //ConfigCreatorTestFuncObject testClass = kernel.Get<ConfigCreatorTestFuncObject>();
                //testClass.GetConfig();
                //EvaluateFunc(testClass.GetConfig(), 2000000, pOptions);

                //Console.ReadLine();

                ICalculatorService calculatorService = kernel.Get<ICalculatorService>();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                int sum = calculatorService.Add(2, 3).Result;
                sw.Stop();

                Console.WriteLine($"Add function evaluation time: {sw.Elapsed.TotalSeconds} sec");
                Console.WriteLine($"Sum: {sum}");

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private static void RunObjectCreationTest<T>(IKernel kernel, int count, ParallelOptions pOptions)
        {
            Console.WriteLine($"Start resolving {count} {typeof(T).BaseType.GetGenericArguments()[0].FullName}");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, count, pOptions, i => kernel.Get<T>());

            sw.Stop();

            Console.WriteLine($"{count} objects created in {sw.Elapsed.TotalSeconds} seconds");
            Console.WriteLine();
        }

        private static void EvaluateFunc<T>(Func<T> func, int count, ParallelOptions pOptions)
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
