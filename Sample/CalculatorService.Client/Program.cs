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
    }
}
