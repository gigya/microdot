using System;
using CalculatorService.Interface;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Ninject;

namespace CalculatorService
{

    class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        public override string ServiceName => "CalculatorService";

        static void Main(string[] args)
        {
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
