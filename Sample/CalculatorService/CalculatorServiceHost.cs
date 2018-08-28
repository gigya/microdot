using System;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using CalculatorService.Interface;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace CalculatorService
{

    class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", Environment.CurrentDirectory);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", "");
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", Environment.CurrentDirectory);
            Environment.SetEnvironmentVariable("DC", "global");
            Environment.SetEnvironmentVariable("ENV", "dev");


            try
            {
                new CalculatorServiceHost().Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        protected override ILoggingModule GetLoggingModule() => new NLogModule();

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Bind<ICalculatorService>().To<CalculatorService>();
        }
    }
}
