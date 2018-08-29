using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CalculatorService.Interface;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Factory;

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

                kernel.Bind<ConfigCreatorTest>().ToSelf().InTransientScope();

                Console.WriteLine("Start test");
                //RunObjectCreationTest(kernel, 2000000);
                ConfigCreatorTest testClass = kernel.Get<ConfigCreatorTest>();
                testClass.GetISourceBlockByFunc();
                EvaluateFunc(testClass, 2000000);

                Console.ReadLine();

                ICalculatorService calculatorService = kernel.Get<ICalculatorService>();
                int sum = calculatorService.Add(2, 3).Result;
                Console.WriteLine($"Sum: {sum}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private static void RunObjectCreationTest(IKernel kernel, int count)
        {
            ParallelOptions pOptions = new ParallelOptions();
            pOptions.MaxDegreeOfParallelism = 4;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, count, pOptions, i => kernel.Get<ConfigCreatorTest>());

            sw.Stop();

            Console.WriteLine($"{count} objects created in {sw.Elapsed.TotalSeconds} seconds");
        }

        private static void EvaluateFunc(ConfigCreatorTest testClass, int count)
        {
            ParallelOptions pOptions = new ParallelOptions();
            pOptions.MaxDegreeOfParallelism = 4;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, count, pOptions, i => testClass.GetISourceBlockByFunc());

            sw.Stop();

            Console.WriteLine($"Function was evaluated {count} times in {sw.Elapsed.TotalSeconds} seconds");
        }
    }
}
